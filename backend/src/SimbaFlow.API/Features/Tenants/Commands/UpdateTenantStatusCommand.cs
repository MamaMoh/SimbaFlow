using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Persistence;

namespace SimbaFlow.API.Features.Tenants.Commands;

public record UpdateTenantStatusCommand(Guid TenantId, TenantStatus Status) : IRequest<Result>;

/// <summary>
/// Suspends an agency, or brings one back.
///
/// Deactivating is not done here: that is offboarding, which records who did it and why, and is
/// the step before an export and a purge. Setting the bare status left an agency that looked
/// offboarded with nothing to say when, by whom, or whether its data had been kept.
/// </summary>
public class UpdateTenantStatusHandler : IRequestHandler<UpdateTenantStatusCommand, Result>
{
    private readonly IPlatformDbContext _context;
    private readonly ITenantSchemaResolver _schemaResolver;
    private readonly ITenantLifecycleService _lifecycle;

    public UpdateTenantStatusHandler(
        IPlatformDbContext context, ITenantSchemaResolver schemaResolver, ITenantLifecycleService lifecycle)
    {
        _context = context;
        _schemaResolver = schemaResolver;
        _lifecycle = lifecycle;
    }

    public async Task<Result> Handle(UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId && !t.IsDeleted, cancellationToken);

        if (tenant is null)
            return Result.Failure("Tenant not found.", 404);

        if (request.Status == TenantStatus.Deactivated)
            return Result.Failure("Use Offboard to deactivate an agency.", 400);

        if (request.Status == TenantStatus.Active && tenant.SubscriptionStatus != TenantStatus.Active)
        {
            if (OffboardingRules.ReactivateRefusalFor(tenant) is { } refusal)
                return Result.Failure(refusal, 409);

            // Startup migrates active agencies only, so one that has been away may be behind.
            // Catch it up before its people are let back in, not on their first page.
            await _lifecycle.PrepareSchemaAsync(tenant.SchemaName, tenant.Id, cancellationToken);

            tenant.OffboardedAt = null;
            tenant.OffboardedBy = null;
            tenant.OffboardingReason = null;
        }

        tenant.SubscriptionStatus = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        _schemaResolver.InvalidateCache(request.TenantId);

        return Result.Success();
    }
}
