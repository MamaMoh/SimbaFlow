using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Offices;

/// <summary>Which office a candidate may be put on.</summary>
public static class OfficeAssignment
{
    public record Outcome(bool IsValid, Guid? OfficeId, string? Error);

    /// <summary>The office must exist and still be open.</summary>
    public static async Task<Outcome> CheckAsync(ITenantDbContext db, Guid officeId, CancellationToken ct)
    {
        var office = await db.Offices.AsNoTracking()
            .Where(o => o.Id == officeId && !o.IsDeleted)
            .Select(o => new { o.IsActive })
            .FirstOrDefaultAsync(ct);

        if (office is null) return new(false, null, "That office no longer exists.");
        if (!office.IsActive) return new(false, null, "That office is closed. Choose an open office.");
        return new(true, officeId, null);
    }

    /// <summary>
    /// The office for a new registration. When none is chosen and the agency has exactly one
    /// open office, that is the one: an agency with a single branch should not have to pick it on
    /// every form, and a blank there would put every candidate under "No office" in the reports.
    /// </summary>
    public static async Task<Outcome> ForNewCandidateAsync(
        ITenantDbContext db, Guid? requested, CancellationToken ct)
    {
        if (requested is Guid id) return await CheckAsync(db, id, ct);

        var open = await db.Offices.AsNoTracking()
            .Where(o => !o.IsDeleted && o.IsActive)
            .Select(o => o.Id)
            .Take(2)
            .ToListAsync(ct);

        return new(true, open.Count == 1 ? open[0] : null, null);
    }
}
