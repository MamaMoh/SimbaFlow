using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Embassy.Commands;

/// <summary>
/// The selected candidates as a Tasheer submission sheet.
///
/// Tasheer appointments are booked for a group at a time and the office wants the batch as one
/// spreadsheet it can hand over, rather than reading names off a screen.
/// </summary>
public record ExportTasheerListCommand(List<Guid> CandidateIds)
    : IRequest<Result<byte[]>>, IRequirePermission
{
    public string RequiredPermission => "embassy.read";
}

public class ExportTasheerListHandler : IRequestHandler<ExportTasheerListCommand, Result<byte[]>>
{
    private readonly ITenantDbContext _context;
    private readonly IReportExportService _export;

    public ExportTasheerListHandler(ITenantDbContext context, IReportExportService export)
    {
        _context = context;
        _export = export;
    }

    public async Task<Result<byte[]>> Handle(ExportTasheerListCommand request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? []).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return Result<byte[]>.Failure("Select at least one candidate", 400);

        var rows = await _context.Candidates.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .Select(c => new
            {
                c.FullName,
                c.PassportNumber,
                c.PassportExpiryDate,
                c.DateOfBirth,
                c.Gender,
                c.PhoneNumber,
                c.CountryOfTravel,
                c.PartnerName,
                c.VisaNumber,
                c.Occupation,
            })
            .ToListAsync(ct);

        if (rows.Count == 0) return Result<byte[]>.Failure("No candidates found", 404);

        var table = new ReportTable(
            Key: "tasheer-list",
            Title: "Tasheer submission list",
            Subtitle: $"{rows.Count} candidate(s) · prepared {DateTime.UtcNow:dd MMM yyyy}",
            Columns:
            [
                new ReportColumn("no", "No."),
                new ReportColumn("name", "Full name"),
                new ReportColumn("passport", "Passport"),
                new ReportColumn("expiry", "Passport expiry"),
                new ReportColumn("dob", "Date of birth"),
                new ReportColumn("gender", "Gender"),
                new ReportColumn("phone", "Phone"),
                new ReportColumn("destination", "Destination"),
                new ReportColumn("partner", "Partner agency"),
                new ReportColumn("visa", "Visa no."),
                new ReportColumn("occupation", "Occupation"),
            ],
            Rows: [.. rows.Select((r, i) => new Dictionary<string, object?>
            {
                ["no"] = i + 1,
                ["name"] = r.FullName,
                ["passport"] = r.PassportNumber,
                ["expiry"] = r.PassportExpiryDate?.ToString("dd/MM/yyyy"),
                ["dob"] = r.DateOfBirth.ToString("dd/MM/yyyy"),
                ["gender"] = r.Gender.ToString(),
                ["phone"] = r.PhoneNumber,
                ["destination"] = r.CountryOfTravel,
                ["partner"] = r.PartnerName,
                ["visa"] = r.VisaNumber,
                ["occupation"] = r.Occupation,
            })],
            GeneratedAtUtc: DateTime.UtcNow);

        return Result<byte[]>.Success(_export.ToExcel(table));
    }
}
