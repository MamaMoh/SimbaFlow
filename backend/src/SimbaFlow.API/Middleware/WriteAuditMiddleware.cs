using Microsoft.AspNetCore.Routing;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Middleware;

/// <summary>
/// The catch-all for the change log. Writes that go through MediatR are recorded by the audit
/// behaviour, and the platform team's inline actions by <c>PlatformAudit</c>; a write endpoint
/// that does neither (partners, offices, branding, bot settings, intake defaults…) used to leave
/// no trace at all. This records any signed-in POST, PUT, PATCH or DELETE that nothing else
/// recorded, by its route, so a new endpoint is on the trail from the day it is added.
/// </summary>
public sealed class WriteAuditMiddleware
{
    /// <summary>The prefix that marks a row written here, and tells the audit page how to word it.</summary>
    public const string ActionPrefix = "HTTP ";

    private readonly RequestDelegate _next;

    public WriteAuditMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IAuditService audit, ICurrentUserService currentUser)
    {
        if (!IsWrite(context.Request.Method) || IsExempt(context.Request.Path))
        {
            await _next(context);
            return;
        }

        var started = System.Diagnostics.Stopwatch.StartNew();
        var failed = false;
        try
        {
            await _next(context);
        }
        catch
        {
            failed = true;
            throw;
        }
        finally
        {
            // Anonymous calls (webhooks, browser error reports) have nobody to attribute.
            if (!audit.HasLogged && currentUser.UserId is not null
                && context.GetEndpoint() is RouteEndpoint endpoint)
            {
                var route = endpoint.RoutePattern.RawText ?? context.Request.Path.Value ?? "";
                await audit.LogAsync(new AuditEntry(
                    UserId: currentUser.UserId,
                    Action: ActionName(context.Request.Method, route),
                    EntityType: EntityType(route),
                    EntityId: EntityId(context.Request.RouteValues),
                    Success: !failed && context.Response.StatusCode < 400,
                    DurationMs: (int)started.ElapsedMilliseconds,
                    IpAddress: currentUser.IpAddress,
                    UserAgent: currentUser.UserAgent,
                    TenantId: currentUser.TenantId), CancellationToken.None);
            }
        }
    }

    public static bool IsWrite(string method) =>
        HttpMethods.IsPost(method) || HttpMethods.IsPut(method)
        || HttpMethods.IsPatch(method) || HttpMethods.IsDelete(method);

    // A token refresh happens on every page and records nothing anyone did; the realtime hub's
    // handshake is not a change either.
    public static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/api/auth/refresh")
        || path.StartsWithSegments("/hubs");

    /// <summary>"PUT /api/partners/{id:guid}" → "HTTP PUT /api/partners/{id}".</summary>
    public static string ActionName(string method, string route) =>
        $"{ActionPrefix}{method.ToUpperInvariant()} /{string.Join('/', Segments(route).Select(s =>
            s.StartsWith('{') ? "{" + s.Trim('{', '}').Split(':', '=', '?')[0] + "}" : s))}";

    /// <summary>The area the route belongs to: "/api/partners/links/{id}/status" → "partners".</summary>
    public static string? EntityType(string route) =>
        Segments(route).FirstOrDefault(s => !s.StartsWith('{') && s != "api");

    public static string? EntityId(RouteValueDictionary values)
    {
        foreach (var key in new[] { "id", "linkId", "documentId" })
            if (values.TryGetValue(key, out var v) && v is not null)
                return v.ToString();

        return values
            .Where(kv => kv.Key.EndsWith("Id", StringComparison.OrdinalIgnoreCase) && kv.Value is not null)
            .Select(kv => kv.Value!.ToString())
            .FirstOrDefault();
    }

    private static IEnumerable<string> Segments(string route) =>
        route.Split('/', StringSplitOptions.RemoveEmptyEntries);
}
