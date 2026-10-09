using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Queries;

public record GetCandidatesQuery(
    int Page, int PageSize, string? Search,
    Guid? StageId, string? CountryOfTravel,
    // null → active only (default). "all" → every status. Otherwise a specific status name.
    string? Status = null,
    int? MinAge = null,
    int? MaxAge = null,
    Guid? PartnerAgencyId = null,
    string? Occupation = null) : IRequest<Result<PaginatedCandidateResult>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record PaginatedCandidateResult(
    List<CandidateListDto> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);

public record CandidateListDto(
    Guid Id,
    string FullName,
    string PassportNumber,
    string? LabourId,
    string? CurrentStageName,
    string? CountryOfTravel,
    string? PartnerName,
    string Status,
    DateTime RegisteredAt,
    string DateOfBirth,
    int? Age,
    string? Occupation,
    string? SponsorName,
    string? SponsorIdNumber,
    string? VisaNumber,
    string? AgentName,
    string? WorksIn,
    string? PhoneNumber,
    string? ContactPerson2,
    string? ContactPhone2,
    int? ExperienceAbroadYears,
    /// <summary>Who removed the record, for the rows the Inactive list now keeps.</summary>
    string? DeletedBy,
    DateTime? DeletedAt,
    /// <summary>
    /// Whether this row's ⋯ menu may offer Delete. Decided here rather than on the page,
    /// because the page has no way to know which stage an agency made its first one.
    /// </summary>
    bool CanDelete);

/// <summary>
/// Active, inactive or all — the one rule, shared by the list and by the filter counts.
///
/// A deleted candidate is not gone, it is inactive. It used to vanish from every list, which on
/// a desk where several people work the same pipeline means the person who goes looking cannot
/// tell a record somebody removed from one that was never registered.
/// </summary>
internal static class CandidateBuckets
{
    internal static IQueryable<Candidate> InStatus(IQueryable<Candidate> query, string? status)
    {
        var bucket = status?.Trim() ?? "";

        if (bucket.Equals("inactive", StringComparison.OrdinalIgnoreCase))
            return query.Where(c => c.IsDeleted || c.Status != CandidateStatus.Active);

        if (bucket.Equals("all", StringComparison.OrdinalIgnoreCase))
            return query;

        if (bucket.Length == 0)
            return query.Where(c => !c.IsDeleted && c.Status == CandidateStatus.Active);

        return Enum.TryParse<CandidateStatus>(bucket, true, out var wanted)
            ? query.Where(c => !c.IsDeleted && c.Status == wanted)
            : query.Where(c => !c.IsDeleted);
    }
}

public class GetCandidatesHandler : IRequestHandler<GetCandidatesQuery, Result<PaginatedCandidateResult>>
{
    private readonly ITenantDbContext _context;

    public GetCandidatesHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PaginatedCandidateResult>> Handle(GetCandidatesQuery request, CancellationToken cancellationToken)
    {
        var query = CandidateBuckets.InStatus(_context.Candidates.AsNoTracking(), request.Status);

        // Searched in the database over every candidate, not in the browser over the page it
        // happens to be holding — a desk with two thousand people on the books was searching the
        // hundred rows the table had loaded and concluding the candidate was not there.
        //
        // Lowered and compared with Contains rather than with ILike. Both become the same scan
        // in Postgres, and this one is also what the in-memory provider the tests run on can
        // execute — a search that silently matches nothing is exactly the failure worth having
        // tests for, and ILike leaves it untestable.
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.Trim().ToLower();

            // The whole name as well as its parts. Names are stored in three columns and typed
            // as one, so "Almaz Kebede" matched nothing: it is in neither FirstName nor LastName.
            query = query.Where(c =>
                (c.FirstName + " " + (c.MiddleName ?? "") + " " + c.LastName).ToLower().Contains(search) ||
                (c.FirstName + " " + c.LastName).ToLower().Contains(search) ||
                c.PassportNumber.ToLower().Contains(search) ||
                (c.LocalFullName != null && c.LocalFullName.ToLower().Contains(search)) ||
                (c.PhoneNumber != null && c.PhoneNumber.ToLower().Contains(search)) ||
                (c.LabourId != null && c.LabourId.ToLower().Contains(search)) ||
                (c.ENumber != null && c.ENumber.ToLower().Contains(search)) ||
                (c.SponsorName != null && c.SponsorName.ToLower().Contains(search)) ||
                (c.VisaNumber != null && c.VisaNumber.ToLower().Contains(search)));
        }

        if (request.StageId.HasValue)
            query = query.Where(c => c.CurrentStageId == request.StageId);

        if (!string.IsNullOrWhiteSpace(request.CountryOfTravel))
            query = query.Where(c => c.CountryOfTravel == request.CountryOfTravel);

        if (request.PartnerAgencyId.HasValue)
            query = query.Where(c => c.PartnerAgencyId == request.PartnerAgencyId);

        if (!string.IsNullOrWhiteSpace(request.Occupation))
            query = query.Where(c => c.Occupation == request.Occupation);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Age is asked for as a range of years and held as a date of birth, so the range is
        // turned into dates rather than the date of birth into an age: an age computed per row
        // cannot use the index, and recomputing it for every candidate to filter a handful is
        // the slow way round. Someone is 25 from their 25th birthday until the day before their
        // 26th, which is what the two bounds say.
        if (request.MinAge is int minAge && minAge >= 0)
        {
            var newestBirthday = today.AddYears(-minAge);
            query = query.Where(c => c.DateOfBirth <= newestBirthday);
        }

        if (request.MaxAge is int maxAge && maxAge >= 0)
        {
            var oldestBirthday = today.AddYears(-(maxAge + 1));
            query = query.Where(c => c.DateOfBirth > oldestBirthday);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // One lookup for the whole page: deletion belongs to the stage a candidate starts in,
        // and every row is measured against the same stage.
        var initialStageId = await _context.WorkflowStages
            .AsNoTracking()
            .Where(s => s.IsInitialStage && !s.IsDeleted)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var items = await query
            .OrderByDescending(c => c.RegisteredAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(c => new CandidateListDto(
                c.Id,
                string.IsNullOrEmpty(c.MiddleName)
                    ? c.FirstName + " " + c.LastName
                    : c.FirstName + " " + c.MiddleName + " " + c.LastName,
                c.PassportNumber,
                c.LabourId,
                c.CurrentStageName,
                c.CountryOfTravel,
                c.PartnerName,
                c.Status.ToString(),
                c.RegisteredAt,
                c.DateOfBirth.ToString("yyyy-MM-dd"),
                today.Year - c.DateOfBirth.Year -
                    ((c.DateOfBirth.Month > today.Month ||
                      (c.DateOfBirth.Month == today.Month && c.DateOfBirth.Day > today.Day)) ? 1 : 0),
                c.Occupation,
                c.SponsorName,
                c.SponsorIdNumber,
                c.VisaNumber,
                c.AgentName,
                c.WorksIn,
                c.PhoneNumber,
                c.ContactPerson2,
                c.ContactPhone2,
                c.ExperienceAbroadYears,
                c.DeletedBy,
                c.DeletedAt,
                c.CurrentStageId == null || c.CurrentStageId == initialStageId))
            .ToListAsync(cancellationToken);

        var totalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize);

        return Result<PaginatedCandidateResult>.Success(
            new PaginatedCandidateResult(items, totalCount, request.Page, request.PageSize, totalPages));
    }
}

/// <summary>
/// What there is to filter the candidate list by, counted over the whole database.
///
/// The stage chips above the list used to be built from the rows the page happened to be
/// holding, so an agency with more candidates than one page saw counts that were a sample and a
/// list of stages with the quiet ones missing. Everything here is counted by the database over
/// every candidate in the chosen bucket, and choosing one narrows the list in the database too.
///
/// <paramref name="Status"/> is the active/inactive/all bucket the list is showing, so the
/// counts describe what the person is actually looking at rather than the whole file.
/// </summary>
public record GetCandidateFilterOptionsQuery(string? Status = null)
    : IRequest<Result<CandidateFilterOptions>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record StageFilterOption(Guid? Id, string Name, int Count);
public record ValueFilterOption(string Value, int Count);
public record PartnerFilterOption(Guid Id, string Name, int Count);

public record CandidateFilterOptions(
    IReadOnlyList<StageFilterOption> Stages,
    IReadOnlyList<ValueFilterOption> Countries,
    IReadOnlyList<PartnerFilterOption> Partners,
    IReadOnlyList<ValueFilterOption> Occupations,
    int Total);

public class GetCandidateFilterOptionsHandler
    : IRequestHandler<GetCandidateFilterOptionsQuery, Result<CandidateFilterOptions>>
{
    private readonly ITenantDbContext _context;

    public GetCandidateFilterOptionsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<CandidateFilterOptions>> Handle(
        GetCandidateFilterOptionsQuery request, CancellationToken ct)
    {
        // The same bucket rule as the list itself, so the counts and the rows agree.
        var query = CandidateBuckets.InStatus(_context.Candidates.AsNoTracking(), request.Status);

        var stages = await query
            .GroupBy(c => new { c.CurrentStageId, c.CurrentStageName })
            .Select(g => new StageFilterOption(
                g.Key.CurrentStageId, g.Key.CurrentStageName ?? "Intake", g.Count()))
            .ToListAsync(ct);

        var countries = await query
            .Where(c => c.CountryOfTravel != null && c.CountryOfTravel != "")
            .GroupBy(c => c.CountryOfTravel!)
            .Select(g => new ValueFilterOption(g.Key, g.Count()))
            .ToListAsync(ct);

        var partners = await query
            .Where(c => c.PartnerAgencyId != null)
            .GroupBy(c => new { c.PartnerAgencyId, c.PartnerName })
            .Select(g => new PartnerFilterOption(
                g.Key.PartnerAgencyId!.Value, g.Key.PartnerName ?? "Partner", g.Count()))
            .ToListAsync(ct);

        var occupations = await query
            .Where(c => c.Occupation != null && c.Occupation != "")
            .GroupBy(c => c.Occupation!)
            .Select(g => new ValueFilterOption(g.Key, g.Count()))
            .ToListAsync(ct);

        var total = await query.CountAsync(ct);

        return Result<CandidateFilterOptions>.Success(new CandidateFilterOptions(
            [.. stages.OrderByDescending(x => x.Count)],
            [.. countries.OrderByDescending(x => x.Count)],
            // Merged by id: the partner's name is snapshotted on each candidate, so a partner
            // renamed halfway through grouped as two partners with the same id.
            [.. partners
                .GroupBy(p => p.Id)
                .Select(g => new PartnerFilterOption(g.Key, g.First().Name, g.Sum(x => x.Count)))
                .OrderByDescending(x => x.Count)],
            [.. occupations.OrderByDescending(x => x.Count)],
            total));
    }
}

/// <summary>One posting abroad as the candidate page and the form read it.</summary>
public record CandidateWorkExperienceDto(string Country, string? Occupation, int? Years);

public record GetCandidateByIdQuery(Guid Id) : IRequest<Result<CandidateDetailDto>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record CandidateDetailDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? LocalFullName,
    string PassportNumber,
    string? LabourId,
    string? BiometricId,
    string? NationalId,
    string DateOfBirth,
    string? PlaceOfBirth,
    int Gender,
    string? Nationality,
    string? Religion,
    string? MaritalStatus,
    int? NumberOfChildren,
    string? Height,
    string? Weight,
    string? PassportType,
    string? PassportPlaceOfIssue,
    string? PassportIssueDate,
    string? PassportExpiryDate,
    string? PhoneNumber,
    string? Email,
    string? Address,
    string? City,
    string? Country,
    string? Region,
    string? Subcity,
    string? Woreda,
    string? HouseNo,
    string? Occupation,
    string? Qualification,
    string? MonthlySalary,
    string? ContractPeriod,
    string? EnglishLevel,
    string? ArabicLevel,
    string? OtherLanguages,
    int? ExperienceAbroadYears,
    string? WorksIn,
    string? ReferenceNo,
    string? Remark,
    string? CookingLevel,
    bool SkillCleaning,
    bool SkillWashing,
    bool SkillCooking,
    bool SkillIroning,
    bool SkillSewing,
    bool SkillArabicCooking,
    bool SkillTutoring,
    bool SkillComputer,
    string? Complexion,
    bool SkillBabysitting,
    bool SkillChildCare,
    string? CountryOfTravel,
    string? PartnerName,
    Guid? PartnerAgencyId,
    string? ContractDate,
    string? PhotoPath,
    string? FullPhotoPath,
    string? VisaNumber,
    string? VisaType,
    string? SponsorName,
    string? SponsorIdNumber,
    string? SponsorPhone,
    string? SponsorAddress,
    string? SponsorArabicName,
    string? AgentName,
    string? ENumber,
    string? FileNo,
    string? WakalaNo,
    string? ContractNo,
    string? StickerVisaNo,
    string? SignedOn,
    string? RelativeName,
    string? RelativePhone,
    string? RelativeKinship,
    string? RelativeGender,
    string? RelativeBirthDate,
    string? RelativeCity,
    string? RelativeRegion,
    string? RelativeSubcity,
    string? RelativeWoreda,
    string? RelativeHouseNo,
    string? ContactPerson2,
    string? ContactPhone2,
    string? CocCenterName,
    string? CertificateNo,
    string? CertifiedDate,
    string? MedicalPlace,
    string? CurrentStageName,
    Guid? CurrentStageId,
    DateTime RegisteredAt,
    string? RegisteredBy,
    IReadOnlyList<string> ExtraSkills,
    IReadOnlyList<CandidateWorkExperienceDto> WorkExperiences,
    /// <summary>
    /// What the enjaze form is still waiting for, named the way the desk's own screens name it.
    /// Empty means it can be printed. Filled in by the handler, not by the projection.
    /// </summary>
    IReadOnlyList<string>? VisaFormMissing = null);

public class GetCandidateByIdHandler : IRequestHandler<GetCandidateByIdQuery, Result<CandidateDetailDto>>
{
    private readonly ITenantDbContext _context;

    public GetCandidateByIdHandler(ITenantDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CandidateDetailDto>> Handle(GetCandidateByIdQuery request, CancellationToken cancellationToken)
    {
        var candidate = await _context.Candidates
            .AsNoTracking()
            .Where(c => c.Id == request.Id && !c.IsDeleted)
            .Select(c => new CandidateDetailDto(
                c.Id, c.FirstName, c.LastName, c.MiddleName, c.LocalFullName,
                c.PassportNumber, c.LabourId, c.BiometricId, c.NationalId,
                c.DateOfBirth.ToString("yyyy-MM-dd"), c.PlaceOfBirth, (int)c.Gender,
                c.Nationality, c.Religion, c.MaritalStatus, c.NumberOfChildren,
                c.Height, c.Weight,
                c.PassportType, c.PassportPlaceOfIssue,
                c.PassportIssueDate.HasValue ? c.PassportIssueDate.Value.ToString("yyyy-MM-dd") : null,
                c.PassportExpiryDate.HasValue ? c.PassportExpiryDate.Value.ToString("yyyy-MM-dd") : null,
                c.PhoneNumber, c.Email,
                c.Address, c.City, c.Country, c.Region, c.Subcity, c.Woreda, c.HouseNo,
                c.Occupation, c.Qualification, c.MonthlySalary, c.ContractPeriod,
                c.EnglishLevel, c.ArabicLevel, c.OtherLanguages, c.ExperienceAbroadYears, c.WorksIn,
                c.ReferenceNo, c.Remark, c.CookingLevel,
                c.SkillCleaning, c.SkillWashing, c.SkillCooking, c.SkillIroning,
                c.SkillSewing, c.SkillArabicCooking, c.SkillTutoring, c.SkillComputer,
                c.Complexion, c.SkillBabysitting, c.SkillChildCare,
                c.CountryOfTravel, c.PartnerName, c.PartnerAgencyId,
                c.ContractDate.HasValue ? c.ContractDate.Value.ToString("yyyy-MM-dd") : null,
                c.PhotoPath, c.FullPhotoPath,
                c.VisaNumber, c.VisaType, c.SponsorName, c.SponsorIdNumber,
                c.SponsorPhone, c.SponsorAddress, c.SponsorArabicName, c.AgentName,
                c.ENumber, c.FileNo, c.WakalaNo, c.ContractNo, c.StickerVisaNo,
                c.SignedOn.HasValue ? c.SignedOn.Value.ToString("yyyy-MM-dd") : null,
                c.RelativeName, c.RelativePhone, c.RelativeKinship, c.RelativeGender,
                c.RelativeBirthDate.HasValue ? c.RelativeBirthDate.Value.ToString("yyyy-MM-dd") : null,
                c.RelativeCity, c.RelativeRegion, c.RelativeSubcity, c.RelativeWoreda, c.RelativeHouseNo,
                c.ContactPerson2, c.ContactPhone2, c.CocCenterName, c.CertificateNo,
                c.CertifiedDate.HasValue ? c.CertifiedDate.Value.ToString("yyyy-MM-dd") : null,
                c.MedicalPlace,
                c.CurrentStageName, c.CurrentStageId,
                c.RegisteredAt, c.RegisteredBy,
                c.ExtraSkills
                    .Where(s => !s.IsDeleted && s.IsSelected)
                    .Select(s => s.Name)
                    .ToArray(),
                c.WorkExperiences
                    .Where(w => !w.IsDeleted)
                    .OrderBy(w => w.SortOrder)
                    .Select(w => new CandidateWorkExperienceDto(w.Country, w.Occupation, w.Years))
                    .ToArray()))
            .FirstOrDefaultAsync(cancellationToken);

        if (candidate is null) return Result<CandidateDetailDto>.Failure("Candidate not found", 404);

        // Worked out here rather than on the page, so the screen and the endpoint that refuses to
        // print cannot disagree about what is missing.
        return Result<CandidateDetailDto>.Success(candidate with
        {
            VisaFormMissing = [.. VisaFormReadiness.Missing(
                candidate.VisaNumber, candidate.ENumber, candidate.SponsorName,
                candidate.SponsorIdNumber, candidate.PassportNumber,
                candidate.PassportIssueDate, candidate.PassportExpiryDate)],
        });
    }
}

public record GetCandidateDocumentsQuery(Guid CandidateId)
    : IRequest<Result<List<CandidateDocumentDto>>>, IRequirePermission
{
    public string RequiredPermission => "candidate.read";
}

public record CandidateDocumentDto(Guid Id, string OriginalFileName, string ContentType, int DocumentType, long FileSizeBytes, DateTime UploadedAt);

public class GetCandidateDocumentsHandler : IRequestHandler<GetCandidateDocumentsQuery, Result<List<CandidateDocumentDto>>>
{
    private readonly ITenantDbContext _context;

    public GetCandidateDocumentsHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<CandidateDocumentDto>>> Handle(GetCandidateDocumentsQuery request, CancellationToken cancellationToken)
    {
        var docs = await _context.CandidateDocuments
            .AsNoTracking()
            .Where(d => d.CandidateId == request.CandidateId && !d.IsDeleted)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new CandidateDocumentDto(d.Id, d.OriginalFileName, d.ContentType, (int)d.DocumentType, d.FileSizeBytes, d.UploadedAt))
            .ToListAsync(cancellationToken);

        return Result<List<CandidateDocumentDto>>.Success(docs);
    }
}

public record GetCandidateTimelineQuery(Guid CandidateId) : IRequest<Result<List<TimelineEntryDto>>>;

public record TimelineEntryDto(Guid Id, int EventType, string? FromStageName, string? ToStageName, string UserName, DateTime Timestamp, string? Notes);

public class GetCandidateTimelineHandler : IRequestHandler<GetCandidateTimelineQuery, Result<List<TimelineEntryDto>>>
{
    private readonly ITenantDbContext _context;

    public GetCandidateTimelineHandler(ITenantDbContext context) => _context = context;

    public async Task<Result<List<TimelineEntryDto>>> Handle(GetCandidateTimelineQuery request, CancellationToken cancellationToken)
    {
        var events = await _context.WorkflowEvents
            .AsNoTracking()
            .Where(e => e.CandidateId == request.CandidateId)
            .OrderByDescending(e => e.Timestamp)
            .Select(e => new TimelineEntryDto(e.Id, (int)e.EventType, e.FromStageName, e.ToStageName, e.UserName, e.Timestamp, e.Notes))
            .ToListAsync(cancellationToken);

        return Result<List<TimelineEntryDto>>.Success(events);
    }
}
