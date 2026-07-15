using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs all write commands to the audit trail.
/// Runs post-handler execution to capture success/failure status.
/// Read queries are not audited for performance.
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

        // Only audit write operations
        if (!IsAuditableCommand(requestName))
            return await next(cancellationToken);

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            await _auditService.LogAsync(new AuditEntry(
                UserId: _currentUser.UserId,
                Action: requestName,
                EntityType: ExtractEntityType(requestName),
                EntityId: ExtractEntityId(response),
                Success: true,
                DurationMs: (int)stopwatch.ElapsedMilliseconds,
                IpAddress: _currentUser.IpAddress,
                UserAgent: _currentUser.UserAgent), cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogWarning(ex, "Auditable command {CommandName} failed for user {UserId}",
                requestName, _currentUser.UserId);

            await _auditService.LogAsync(new AuditEntry(
                UserId: _currentUser.UserId,
                Action: requestName,
                EntityType: ExtractEntityType(requestName),
                EntityId: null,
                Success: false,
                DurationMs: (int)stopwatch.ElapsedMilliseconds,
                IpAddress: _currentUser.IpAddress,
                UserAgent: _currentUser.UserAgent), cancellationToken);

            throw;
        }
    }

    private static bool IsAuditableCommand(string name) =>
        name.StartsWith("Create", StringComparison.Ordinal) ||
        name.StartsWith("Update", StringComparison.Ordinal) ||
        name.StartsWith("Delete", StringComparison.Ordinal) ||
        name.StartsWith("Set", StringComparison.Ordinal) ||
        name.Contains("Toggle", StringComparison.Ordinal) ||
        name.Contains("Assign", StringComparison.Ordinal) ||
        name.Contains("Reset", StringComparison.Ordinal) ||
        name.Contains("Revoke", StringComparison.Ordinal) ||
        name.Contains("Import", StringComparison.Ordinal);

    private static string? ExtractEntityType(string commandName)
    {
        // Extract entity from "CreateUserCommand" → "User"
        var suffixes = new[] { "Command", "Query" };
        var prefixes = new[] { "Create", "Update", "Delete", "Set", "Toggle", "Assign", "Reset", "Import", "Revoke", "Force" };

        var name = commandName;
        foreach (var suffix in suffixes)
            if (name.EndsWith(suffix, StringComparison.Ordinal))
                name = name[..^suffix.Length];

        foreach (var prefix in prefixes)
            if (name.StartsWith(prefix, StringComparison.Ordinal))
                return name[prefix.Length..];

        return name;
    }

    private static string? ExtractEntityId(TResponse? response)
    {
        if (response is null) return null;

        // Try to extract Id from Result<Guid>
        var dataProperty = response.GetType().GetProperty("Data");
        if (dataProperty?.GetValue(response) is Guid guidValue)
            return guidValue.ToString();

        return null;
    }
}
