using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Travel;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Features.SampleData;

public record SeedSampleDataCommand : IRequest<Result<SampleDataSummary>>, IRequirePermission
{
    public string RequiredPermission => "candidate.create";
}

public record SampleDataSummary(int Created, int Skipped, IReadOnlyList<string> Placements);

/// <summary>
/// Fills the pipeline with candidates for testing — one or more sitting on every board, including
/// the two mirror views and both exception types.
///
/// Each candidate is registered through the ordinary register command and then walked forward with
/// the workflow engine, exactly as staff would. Writing stages and statuses straight into the table
/// would be quicker but would produce data no real action could have produced: no event history, no
/// mirror visibility, and boards that disagree with the timeline. Test data that behaves
/// differently from real data is worse than none.
/// </summary>
public class SeedSampleDataHandler : IRequestHandler<SeedSampleDataCommand, Result<SampleDataSummary>>
{
    private readonly ISender _sender;
    private readonly ITenantDbContext _context;
    private readonly IPlatformDbContext _platform;
    private readonly IWorkflowEngineService _engine;
    private readonly ICurrentUserService _currentUser;

    public SeedSampleDataHandler(
        ISender sender,
        ITenantDbContext context,
        IPlatformDbContext platform,
        IWorkflowEngineService engine,
        ICurrentUserService currentUser)
    {
        _sender = sender;
        _context = context;
        _platform = platform;
        _engine = engine;
        _currentUser = currentUser;
    }

    public async Task<Result<SampleDataSummary>> Handle(SeedSampleDataCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(_currentUser.UserId, out var userId))
            return Result<SampleDataSummary>.Failure("Sign in again — no user context.", 401);
        var userName = _currentUser.UserName ?? "sample-data";

        var stages = await _context.WorkflowStages.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Name, s => s.Id, ct);

        if (stages.Count == 0)
            return Result<SampleDataSummary>.Failure(
                "No workflow is set up for this agency yet, so there are no stages to place anyone in.", 400);

        var rules = await _context.WorkflowTransitionRules.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .ToListAsync(ct);

        Guid? RuleTo(Guid fromStage, string label) => rules
            .FirstOrDefault(r => r.SourceStageId == fromStage && r.ButtonLabel == label)?.Id;

        // Attach a real partner where the agency has one, so the generated contract names a party.
        var partnerId = await FindUsablePartnerAsync(ct);

        var existing = await _context.Candidates.AsNoTracking()
            .Where(c => c.ApplicationNo != null && c.ApplicationNo.StartsWith(SampleDataSpec.Prefix))
            .Select(c => c.ApplicationNo!)
            .ToListAsync(ct);

        var created = 0;
        var skipped = 0;
        var placements = new List<string>();

        for (var i = 0; i < SampleDataSpec.People.Length; i++)
        {
            var p = SampleDataSpec.People[i];
            if (existing.Contains(p.ApplicationNo)) { skipped++; continue; }

            var register = new RegisterCandidateCommand(
                FirstName: p.FirstName,
                LastName: p.LastName,
                MiddleName: p.MiddleName,
                PassportNumber: p.Passport,
                DateOfBirth: p.BirthDate,
                Gender: p.Gender,
                Nationality: "Ethiopian",
                PhoneNumber: p.Phone,
                Email: null,
                Address: "Bole",
                City: p.City,
                Country: "Ethiopia",
                LabourId: $"EF{7710000 + i}",
                CountryOfTravel: "Saudi Arabia",
                PartnerName: null,
                PartnerAgencyId: partnerId,
                ContractDate: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30 + i)).ToString("yyyy-MM-dd"),
                Intake: SampleDataSpec.Intake(p, i));

            var result = await _sender.Send(register, ct);
            if (!result.IsSuccess)
            {
                placements.Add($"{p.ApplicationNo} {p.FirstName}: not created — {result.Error}");
                continue;
            }

            var id = result.Data;
            await WalkAsync(id, p, stages, RuleTo, userId, userName, ct);
            created++;
            placements.Add($"{p.ApplicationNo} {p.FirstName} {p.LastName} → {p.Note}");
        }

        return Result<SampleDataSummary>.Success(new SampleDataSummary(created, skipped, placements));
    }

    /// <summary>Walks one candidate from Intake to wherever the spec says they belong.</summary>
    private async Task WalkAsync(
        Guid id, SamplePerson p, Dictionary<string, Guid> stages,
        Func<Guid, string, Guid?> ruleTo, Guid userId, string userName, CancellationToken ct)
    {
        var t = p.Target;
        if (t == SampleTarget.Intake) return;

        // Everyone else leaves Intake.
        await MoveAsync(id, stages, ruleTo, "Intake", "To New Contracts", userId, userName, ct);

        if (t == SampleTarget.NewContractsPending)
        {
            await _engine.UpdateStatusAsync(id, "status", "Pending Review", userId, userName, ct: ct);
            return;
        }

        await _engine.UpdateStatusAsync(id, "status", "Ready", userId, userName, ct: ct);
        if (t == SampleTarget.NewContractsReady) return;

        await MoveAsync(id, stages, ruleTo, "New Contracts", "To Embassy", userId, userName, ct);

        switch (t)
        {
            case SampleTarget.EmbassyFresh:
                await _engine.UpdateStatusAsync(id, "medical", "Pending", userId, userName, ct: ct);
                await _engine.UpdateStatusAsync(id, "tasheer", "Pending", userId, userName, ct: ct);
                return;

            case SampleTarget.EmbassyMedicalFit:
                await SetMedicalFitAsync(id, userId, userName, ct);
                await _engine.UpdateStatusAsync(id, "tasheer", "Booked", userId, userName, ct: ct);
                return;

            case SampleTarget.EmbassyUnfit:
                await _engine.UpdateStatusAsync(id, "medical", "Booked", userId, userName, ct: ct);
                await _engine.UpdateStatusAsync(id, "medical", "Unfit", userId, userName, ct: ct);
                // An unfit result takes the candidate out of the active pipeline.
                var unfit = await _context.Candidates.FirstAsync(c => c.Id == id, ct);
                unfit.Status = CandidateStatus.Inactive;
                await _context.SaveChangesAsync(ct);
                return;

            case SampleTarget.EmbassyVisaReady:
                await SetMedicalFitAsync(id, userId, userName, ct);
                await _engine.UpdateStatusAsync(id, "tasheer", "Book Done", userId, userName, ct: ct);
                // Ready mirrors the candidate onto the Case Executive board.
                await _engine.UpdateStatusAsync(id, "visa", "Ready", userId, userName, ct: ct);
                return;

            case SampleTarget.EmbassyMirrorsToLmis:
                await SetMedicalFitAsync(id, userId, userName, ct);
                // Fit + Book Done mirrors onto LMIS while the candidate stays in Embassy.
                await _engine.UpdateStatusAsync(id, "tasheer", "Book Done", userId, userName, ct: ct);
                return;
        }

        // Everyone past Embassy has a clean medical, a done tasheer and an issued visa.
        await SetMedicalFitAsync(id, userId, userName, ct);
        await _engine.UpdateStatusAsync(id, "tasheer", "Book Done", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "visa", "Submitted", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "visa", "Issued", userId, userName, ct: ct);
        await MoveAsync(id, stages, ruleTo, "Embassy", "To LMIS", userId, userName, ct);

        if (t == SampleTarget.LmisInsuranceUnpaid)
        {
            await _engine.UpdateStatusAsync(id, "insurance", "Insurance Unpaid", userId, userName, ct: ct);
            return;
        }

        await _engine.UpdateStatusAsync(id, "insurance", "Insurance Paid", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "milestone", "Uploaded", userId, userName, ct: ct);
        if (t == SampleTarget.LmisUploaded) return;

        await _engine.UpdateStatusAsync(id, "milestone", "Check Verified", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "milestone", "Issued", userId, userName, ct: ct);
        await MoveAsync(id, stages, ruleTo, "LMIS", "To Ticket", userId, userName, ct);

        if (t == SampleTarget.Ticket)
        {
            await _engine.UpdateStatusAsync(id, "ticket_status", "Pending", userId, userName, ct: ct);
            return;
        }

        // The transition to Departure requires the booking fields, not just the status.
        await _engine.UpdateStatusAsync(
            id, "ticket_status", "Booking Complete", userId, userName,
            metadata: new Dictionary<string, string>
            {
                ["destination"] = "Riyadh",
                ["flight_date"] = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(9)).ToString("yyyy-MM-dd"),
                ["airline"] = "Ethiopian Airlines",
                ["pnr"] = $"ET{Random.Shared.Next(1000, 9999)}"
            },
            ct: ct);
        await MoveAsync(id, stages, ruleTo, "Ticket", "To Departure", userId, userName, ct);

        if (t == SampleTarget.Departure)
        {
            await _engine.UpdateStatusAsync(id, "notification_status", "Notified", userId, userName, ct: ct);
            return;
        }

        await _engine.UpdateStatusAsync(id, "notification_status", "Notified", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "departure_status", "Departed", userId, userName, ct: ct);
        await MoveAsync(id, stages, ruleTo, "Departure", "To Arrival", userId, userName, ct);

        switch (t)
        {
            case SampleTarget.ArrivalPending:
                await _engine.UpdateStatusAsync(id, "status", "Pending", userId, userName, ct: ct);
                return;

            case SampleTarget.ArrivalRunaway:
                await _engine.UpdateStatusAsync(id, "status", "Runaway", userId, userName, ct: ct);
                await OpenExceptionAsync(id, ExceptionType.Runaway, userId, ct);
                return;

            case SampleTarget.ArrivalReturned:
                await _engine.UpdateStatusAsync(id, "status", "Returned", userId, userName, ct: ct);
                await OpenExceptionAsync(id, ExceptionType.Returned, userId, ct);
                return;

            default:
                await _engine.UpdateStatusAsync(id, "status", "Arrived", userId, userName, ct: ct);
                await MoveAsync(id, stages, ruleTo, "Arrival", "Add to Commission", userId, userName, ct);
                await _engine.UpdateStatusAsync(id, "status", "Pending", userId, userName, ct: ct);
                return;
        }
    }

    private async Task SetMedicalFitAsync(Guid id, Guid userId, string userName, CancellationToken ct)
    {
        await _engine.UpdateStatusAsync(id, "medical", "Booked", userId, userName, ct: ct);
        await _engine.UpdateStatusAsync(id, "medical", "Fit", userId, userName, ct: ct);
    }

    private async Task MoveAsync(
        Guid id, Dictionary<string, Guid> stages, Func<Guid, string, Guid?> ruleTo,
        string fromStage, string label, Guid userId, string userName, CancellationToken ct)
    {
        if (!stages.TryGetValue(fromStage, out var from)) return;
        if (ruleTo(from, label) is not Guid ruleId) return;
        await _engine.ExecuteTransitionAsync(id, ruleId, userId, userName, "Sample data", ct);
    }

    private async Task OpenExceptionAsync(Guid id, ExceptionType type, Guid userId, CancellationToken ct)
    {
        _context.ExceptionCases.Add(new ExceptionCase
        {
            CandidateId = id,
            Type = type,
            Status = ExceptionStatus.Open,
            OpenedAt = DateTime.UtcNow,
            OpenedByUserId = userId,
            FinancialImpactAmount = type == ExceptionType.Runaway ? 25000 : 12000,
            FinancialImpactCurrency = "ETB"
        });
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>The first partner this agency holds a live agreement with, if any.</summary>
    private async Task<Guid?> FindUsablePartnerAsync(CancellationToken ct)
    {
        if (_currentUser.TenantId is not Guid tid) return null;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var candidates = await _platform.PartnerLinks.AsNoTracking()
            .Where(l => l.TenantId == tid && !l.IsDeleted
                        && l.AgreementStart <= today && l.AgreementEnd >= today)
            .Select(l => l.PartnerAgencyId)
            .ToListAsync(ct);

        foreach (var pid in candidates)
        {
            var check = await Features.Partners.PartnerLinkValidator.CheckAsync(_platform, tid, pid, ct);
            if (check.IsValid) return pid;
        }

        return null;
    }
}
