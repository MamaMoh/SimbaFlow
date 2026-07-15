using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Tenants.Commands;

public record UpdateTenantStatusCommand(Guid TenantId, TenantStatus Status) : IRequest<Result>;

public class UpdateTenantStatusHandler : IRequestHandler<UpdateTenantStatusCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantSchemaResolver _schemaResolver;

    public UpdateTenantStatusHandler(IApplicationDbContext context, ITenantSchemaResolver schemaResolver)
    {
        _context = context;
        _schemaResolver = schemaResolver;
    }

    public async Task<Result> Handle(UpdateTenantStatusCommand request, CancellationToken cancellationToken)
    {
        var tenant = await _context.Tenants
            .FirstOrDefaultAsync(t => t.Id == request.TenantId, cancellationToken);

        if (tenant is null)
            return Result.Failure("Tenant not found.", 404);

        tenant.SubscriptionStatus = request.Status;
        await _context.SaveChangesAsync(cancellationToken);

        // Invalidate cached schema resolution
        _schemaResolver.InvalidateCache(request.TenantId);

        return Result.Success();
    }
}
