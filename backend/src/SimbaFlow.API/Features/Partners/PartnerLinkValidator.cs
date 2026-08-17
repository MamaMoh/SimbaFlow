using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Partners;

public record PartnerLinkCheck(bool IsValid, string? Error, string? PartnerName, string? CountryName);

/// <summary>
/// Validates that a candidate may be placed through a given partner agency: the agency must hold a
/// live agreement (ትስስር) with that partner. Without this a candidate could be attached to any
/// partner in the shared catalog — including one the agency has no agreement with, or a lapsed one.
/// </summary>
public static class PartnerLinkValidator
{
    public static async Task<PartnerLinkCheck> CheckAsync(
        IPlatformDbContext platform,
        Guid? tenantId,
        Guid? partnerAgencyId,
        CancellationToken ct)
    {
        // No partner supplied → nothing to validate (the field is optional at intake).
        if (partnerAgencyId is not Guid partnerId || partnerId == Guid.Empty)
            return new PartnerLinkCheck(true, null, null, null);

        if (tenantId is not Guid tid || tid == Guid.Empty)
            return new PartnerLinkCheck(false, "Tenant context is required to assign a partner.", null, null);

        var row = await (
            from link in platform.PartnerLinks.AsNoTracking()
            join p in platform.PartnerAgencies.AsNoTracking() on link.PartnerAgencyId equals p.Id
            where link.TenantId == tid && link.PartnerAgencyId == partnerId
                  && !link.IsDeleted && !p.IsDeleted
            select new
            {
                p.Name,
                p.CountryName,
                link.AgreementStart,
                link.AgreementEnd,
                link.Status,
            }).FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return new PartnerLinkCheck(
                false,
                "Your agency has no agreement with the selected partner. Link the partner under Partners first.",
                null, null);
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var state = PartnerAgreementRules.Evaluate(
            row.AgreementStart, row.AgreementEnd, row.Status, today);

        if (!PartnerAgreementRules.IsUsableForIntake(state))
        {
            var days = PartnerAgreementRules.DaysRemaining(row.AgreementEnd, today);
            return new PartnerLinkCheck(
                false,
                $"The agreement with “{row.Name}” cannot be used: {PartnerAgreementRules.Describe(state, days)}. Renew it under Partners.",
                row.Name, row.CountryName);
        }

        return new PartnerLinkCheck(true, null, row.Name, row.CountryName);
    }
}
