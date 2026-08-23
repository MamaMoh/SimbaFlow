using System.Net;
using FluentValidation;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Middleware;

public class GlobalExceptionHandler
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(RequestDelegate next, ILogger<GlobalExceptionHandler> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation error occurred");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";

            var errors = ex.Errors.Select(e => new { e.PropertyName, e.ErrorMessage });
            var response = new { IsSuccess = false, Error = "Validation failed", Errors = errors };
            await context.Response.WriteAsJsonAsync(response);
        }
        catch (ForbiddenAccessException ex)
        {
            _logger.LogWarning(ex, "Forbidden access");
            context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { IsSuccess = false, Error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Unauthorized access");
            context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { IsSuccess = false, Error = "Unauthorized" });
        }
        catch (BadHttpRequestException ex)
        {
            // Malformed request (missing/unparseable route or query binding, bad JSON body).
            // This is the caller's mistake, so report 400 rather than a misleading 500.
            _logger.LogWarning(ex, "Bad request");
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { IsSuccess = false, Error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");

            // Persist it so a 500 is discoverable without someone tailing container logs.
            // Resolved per-request because the tracker touches the database.
            try
            {
                var tracker = context.RequestServices.GetService<IErrorTracker>();
                if (tracker is not null)
                    await tracker.CaptureAsync(ex, context.Request.Path, context.Request.Method, 500);
            }
            catch
            {
                // Recording must never replace the response the caller is owed.
            }

            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { IsSuccess = false, Error = "An internal server error occurred" });
        }
    }
}
