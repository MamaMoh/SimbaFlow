using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Embassy.Commands;

/// <summary>
/// The selected candidates as a Tasheer submission sheet.
///
/// This is not a report. It is the file the Tasheer system is given, so its columns are that
/// system's columns in that system's order, headings and asterisks included — the asterisks are
/// how the template marks a required field, and an importer matching headings by text does not
/// know that "First Name" and "First Name*" are the same column. Nothing is added for a reader's
/// benefit: no title above the headings, no running number down the side, no filter dropdowns.
/// A person looking at this file is looking at it to check it before uploading.
///
/// Dates go out as yyyy-MM-dd. The desk's own forms read dd/MM/yyyy, which is the same eleven
/// days a month written the other way round, and a system that reads it the way it is written
/// turns the 7th of September into the 9th of July without complaining.
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
    private readonly IDocumentBrandingService _branding;

    public ExportTasheerListHandler(
        ITenantDbContext context,
        IReportExportService export,
        IDocumentBrandingService branding)
    {
        _context = context;
        _export = export;
        _branding = branding;
    }

    /// <summary>
    /// The Tasheer template's headings, in its order. Changed only to match a change there.
    /// </summary>
    private static readonly List<ReportColumn> Columns =
    [
        new("eNo", "E.No"),
        new("firstName", "First Name*"),
        new("secondName", "Second Name"),
        new("lastName", "Last Name*"),
        new("passport", "Passport Number*"),
        new("dob", "Date of Birth*"),
        new("nationality", "Nationality*"),
        new("issued", "Date of Issue*"),
        new("gender", "Gender*"),
        new("placeOfIssue", "Place of Issue*"),
        new("expiry", "Expiry Date*"),
        new("mobile", "Applicant Mobile No.*"),
        new("email", "Email ID*"),
    ];

    public async Task<Result<byte[]>> Handle(ExportTasheerListCommand request, CancellationToken ct)
    {
        var ids = (request.CandidateIds ?? []).Where(i => i != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return Result<byte[]>.Failure("Select at least one candidate", 400);

        var candidates = await _context.Candidates.AsNoTracking()
            .Where(c => ids.Contains(c.Id) && !c.IsDeleted)
            .OrderBy(c => c.LastName).ThenBy(c => c.FirstName)
            .ToListAsync(ct);

        if (candidates.Count == 0) return Result<byte[]>.Failure("No candidates found", 404);

        // Tasheer writes to one address about the whole batch, and it is the agency's — the
        // candidates do not have mailboxes to answer from.
        var agency = await _branding.GetAgencyIdentityAsync(ct);

        var table = new ReportTable(
            Key: "tasheer-list",
            Title: "Tasheer",
            Subtitle: null,
            Columns: Columns,
            Rows: [.. candidates.Select(c => Row(c, agency))],
            GeneratedAtUtc: DateTime.UtcNow,
            ForImport: true);

        return Result<byte[]>.Success(_export.ToExcel(table));
    }

    private static Dictionary<string, object?> Row(Candidate c, AgencyIdentity agency)
    {
        // From the whole name rather than the stored parts: a candidate registered with two names
        // in the first box has their father's name in it, and the sheet has to put it in the
        // second. See PersonName.
        var name = PersonName.Split(c.FullName);

        return new Dictionary<string, object?>
        {
            ["eNo"] = c.ENumber,
            ["firstName"] = name.First,
            ["secondName"] = name.Second,
            ["lastName"] = name.Last,
            ["passport"] = c.PassportNumber,
            ["dob"] = Iso(c.DateOfBirth),
            ["nationality"] = Blank(c.Nationality) ? "Ethiopia" : c.Nationality!.Trim(),
            ["issued"] = Iso(c.PassportIssueDate),
            ["gender"] = c.Gender.ToString(),
            ["placeOfIssue"] = (Blank(c.PassportPlaceOfIssue) ? "Ethiopia" : c.PassportPlaceOfIssue!)
                .Trim().ToUpperInvariant(),
            ["expiry"] = Iso(c.PassportExpiryDate),
            ["mobile"] = c.PhoneNumber,
            ["email"] = Blank(c.Email) ? agency.Email : c.Email!.Trim(),
        };
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// A date as text, not as a date.
    ///
    /// Written into a date cell, Excel stores it as a number and shows it in whatever format the
    /// machine that opens it prefers — so the file the desk checks and the file Tasheer reads can
    /// disagree about what is in it. As text it says yyyy-MM-dd to everyone.
    /// </summary>
    private static string? Iso(DateOnly? date) => date?.ToString("yyyy-MM-dd");
}
