using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Tenants.Queries;

public record GetTenantsQuery : IRequest<Result<List<TenantListDto>>>;

public record TenantListDto(
    Guid Id,
    string Name,
    string Slug,
    string SchemaName,
    string ContactEmail,
    TenantStatus Status,
    DateTime ProvisionedAt);

public class GetTenantsHandler : IRequestHandler<GetTenantsQuery, Result<List<TenantListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetTenantsHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<TenantListDto>>> Handle(GetTenantsQuery request, CancellationToken cancellationToken)
    {
        var tenants = await _context.Tenants
            .AsNoTracking()
            .Where(t => !t.IsDeleted)
            .OrderBy(t => t.Name)
            .Select(t => new TenantListDto(
                t.Id, t.Name, t.Slug, t.SchemaName,
                t.ContactEmail, t.SubscriptionStatus, t.ProvisionedAt))
            .ToListAsync(cancellationToken);

        return Result<List<TenantListDto>>.Success(tenants);
    }
}
