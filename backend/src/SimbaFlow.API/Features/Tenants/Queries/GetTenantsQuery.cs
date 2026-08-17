using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Tenants.Queries;

public record GetTenantsQuery : IRequest<Result<List<TenantListDto>>>;

public record TenantListDto(
    Guid Id,
    string Name,
    string Slug,
    string SchemaName,
    string ContactEmail,
    TenantStatus Status,
    DateTime ProvisionedAt,
    int AgencyLevel,
    int MaxPartnersPerCountry,
    int? MaxCountries,
    string? LicenseNumber,
    int LicensedCountryCount);

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, Result<List<TenantListDto>>>
{
    private readonly IPlatformDbContext _context;

    public GetTenantsHandler(IPlatformDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TenantListDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var rows = await _context.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .ToListAsync(cancellationToken);

        var tenants = rows.Select(t =>
        {
            var (perCountry, maxCountries) = AgencyLevelRules.GetCaps(t.AgencyLevel);
            return new TenantListDto(
                t.Id, t.Name, t.Slug, t.SchemaName,
                t.ContactEmail, t.SubscriptionStatus, t.ProvisionedAt,
                t.AgencyLevel, perCountry, maxCountries,
                t.LicenseNumber, t.LicensedCountries?.Count ?? 0);
        }).ToList();

        return Result<List<TenantListDto>>.Success(tenants);
    }
}
