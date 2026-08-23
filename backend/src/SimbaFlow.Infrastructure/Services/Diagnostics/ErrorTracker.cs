using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Audit;

namespace SimbaFlow.Infrastructure.Services.Diagnostics;

public sealed class ErrorTracker : IErrorTracker
{
    private const int MaxMessage = 2000;
    private const int MaxStack = 8000;

    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<ErrorTracker> _logger;

    public ErrorTracker(
        IPlatformDbContext platform,
        ICurrentUserService currentUser,
        ILogger<ErrorTracker> logger)
    {
        _platform = platform;
        _currentUser = currentUser;
        _logger = logger;
    }

    public Task CaptureAsync(
        Exception exception, string? path = null, string? method = null,
        int? statusCode = null, string source = "api", CancellationToken ct = default)
        => SaveAsync(
            exception.GetType().Name,
            exception.Message,
            exception.StackTrace,
            path, method, statusCode, source, ct);

    public Task CaptureClientAsync(string message, string? stack, string? path, CancellationToken ct = default)
        => SaveAsync("ClientError", message, stack, path, "GET", null, "web", ct);

    private async Task SaveAsync(
        string type, string message, string? stack,
        string? path, string? method, int? statusCode, string source, CancellationToken ct)
    {
        try
        {
            Guid? userId = Guid.TryParse(_currentUser.UserId, out var uid) ? uid : null;

            _platform.ErrorEvents.Add(new ErrorEvent
            {
                Fingerprint = Fingerprint(type, message, stack),
                Source = source,
                ExceptionType = Truncate(type, 256),
                Message = Truncate(message, MaxMessage),
                StackTrace = Truncate(stack, MaxStack),
                Path = Truncate(path, 512),
                Method = Truncate(method, 16),
                StatusCode = statusCode,
                UserId = userId,
                TenantId = _currentUser.TenantId,
                OccurredAt = DateTime.UtcNow,
            });

            await _platform.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Swallow deliberately. If the database is the thing that is broken, throwing here
            // would replace a useful error with a confusing one and could mask the original.
            _logger.LogError(ex, "Could not record an error event");
        }
    }

    /// <summary>
    /// Groups identical faults. Only the first stack frame is used: deeper frames vary by request
    /// and would split one recurring bug into hundreds of separate-looking problems.
    /// </summary>
    private static string Fingerprint(string type, string message, string? stack)
    {
        var firstFrame = stack?.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "";
        var raw = $"{type}|{message}|{firstFrame}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))[..16];
    }

    private static string? Truncate(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..max];
}
