using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that logs requests exceeding 500ms threshold.
/// Pass-through only — never blocks or modifies the request/response.
/// </summary>
public class PerformanceLogBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceLogBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;
    private const int ThresholdMs = 500;

    public PerformanceLogBehavior(
        ILogger<PerformanceLogBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next(cancellationToken);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > ThresholdMs)
        {
            _logger.LogWarning(
                "Slow request detected: {RequestName} took {ElapsedMs}ms (threshold: {ThresholdMs}ms). UserId: {UserId}",
                typeof(TRequest).Name,
                stopwatch.ElapsedMilliseconds,
                ThresholdMs,
                _currentUser.UserId ?? "anonymous");
        }

        return response;
    }
}
