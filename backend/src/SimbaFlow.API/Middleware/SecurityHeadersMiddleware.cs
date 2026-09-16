namespace SimbaFlow.API.Middleware;

/// <summary>
/// Security headers for responses from the API itself.
///
/// next.config.mjs sets a thorough header set, but that only covers responses served by the Next.js
/// origin. This API is reachable directly, and it returns uploaded files with a caller-supplied
/// content type — so without nosniff, an uploaded document can be made to render as HTML on the
/// API's own origin. Content-Disposition: attachment was the only thing standing in the way.
///
/// The policy here is deliberately strict: the API serves JSON and file downloads, never a page
/// with scripts or styles of its own, so there is nothing to allow.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";

        // The API reference is a real HTML page with its own scripts and styles, and it is the one
        // thing this API serves that a "load nothing" policy would break. It is mapped only in
        // development, but the exclusion is explicit rather than relying on that.
        if (!IsApiReference(context.Request.Path))
            headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        return _next(context);
    }

    private static bool IsApiReference(PathString path) =>
        path.StartsWithSegments("/scalar", StringComparison.OrdinalIgnoreCase)
        || path.StartsWithSegments("/openapi", StringComparison.OrdinalIgnoreCase);
}
