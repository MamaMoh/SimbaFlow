using MediatR;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that records successful reads of personal records
/// (requests marked <see cref="IAuditedRead"/>) to the read-audit log.
/// Publishing is fire-and-forget to a channel, so the request is never slowed or failed by it.
/// A failed or refused read showed nothing and is not logged.
/// </summary>
public class ReadAuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly IReadAuditService _readAudit;
    private readonly ICurrentUserService _currentUser;

    public ReadAuditBehavior(IReadAuditService readAudit, ICurrentUserService currentUser)
    {
        _readAudit = readAudit;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next(cancellationToken);

        if (request is not IAuditedRead read || !Succeeded(response))
            return response;

        var action = typeof(TRequest).Name;
        var ids = read.AuditEntityIds.Count == 0
            ? new string?[] { null }
            : read.AuditEntityIds.Distinct().Select(id => (string?)id.ToString());

        foreach (var id in ids)
        {
            _readAudit.LogAccess(new ReadAuditEntry(
                UserId: _currentUser.UserId,
                EntityType: read.AuditEntityType,
                EntityId: id,
                Action: action,
                IpAddress: _currentUser.IpAddress,
                UserAgent: _currentUser.UserAgent,
                TenantId: _currentUser.TenantId));
        }

        return response;
    }

    // Result and Result<T> share no interface; anything that is not a Result counts as success.
    private static bool Succeeded(TResponse response) =>
        response?.GetType().GetProperty("IsSuccess")?.GetValue(response) is not false;
}
