using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that catches DbUpdateConcurrencyException
/// and returns a standardized 409 Conflict response.
/// Prevents silent data overwrites in concurrent clinical workflows.
/// </summary>
public class ConcurrencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<ConcurrencyBehavior<TRequest, TResponse>> _logger;

    public ConcurrencyBehavior(ILogger<ConcurrencyBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict detected for {RequestType}. Another user modified this record.",
                typeof(TRequest).Name);

            // If the response type is Result<T>, return a 409 failure
            var responseType = typeof(TResponse);
            if (responseType.IsGenericType && responseType.GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = responseType.GetMethod("Failure", [typeof(string), typeof(int)]);
                if (failureMethod != null)
                {
                    return (TResponse)failureMethod.Invoke(null,
                        ["This record was modified by another user. Please refresh and try again.", 409])!;
                }
            }

            throw;
        }
    }
}
