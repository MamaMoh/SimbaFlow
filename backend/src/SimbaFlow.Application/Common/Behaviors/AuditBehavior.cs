using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that writes every request that changes something — or takes data
/// out — to the audit trail, with who, where, which record and what was asked for.
/// It runs first, so attempts that validation or a missing permission refused are recorded too,
/// as failures. Reads are not recorded here; personal-record reads go to the read-audit log.
/// </summary>
public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;

    public AuditBehavior(
        IAuditService auditService,
        ICurrentUserService currentUser,
        ILogger<AuditBehavior<TRequest, TResponse>> logger)
    {
        _auditService = auditService;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        if (!AuditDetails.IsRecorded(requestName))
            return await next(cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var details = AuditDetails.Describe(request);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            await _auditService.LogAsync(Entry(
                requestName, AuditDetails.EntityId(request, response),
                AuditDetails.Succeeded(response), stopwatch, details), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex, "Auditable command {CommandName} failed for user {UserId}",
                requestName, _currentUser.UserId);

            await _auditService.LogAsync(Entry(
                requestName, AuditDetails.EntityId(request, null),
                success: false, stopwatch, details), CancellationToken.None);

            throw;
        }
    }

    private AuditEntry Entry(
        string requestName, string? entityId, bool success, Stopwatch stopwatch, string? details) =>
        new(
            UserId: _currentUser.UserId,
            Action: requestName,
            EntityType: AuditDetails.EntityType(requestName),
            EntityId: entityId,
            Success: success,
            DurationMs: (int)stopwatch.ElapsedMilliseconds,
            NewValues: details,
            IpAddress: _currentUser.IpAddress,
            UserAgent: _currentUser.UserAgent,
            TenantId: _currentUser.TenantId);
}
