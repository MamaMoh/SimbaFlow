namespace SimbaFlow.Application.Common.Interfaces;

public interface IErrorTracker
{
    /// <summary>
    /// Records a failure. Never throws: error tracking must not become a second source of errors,
    /// and a failure to record must not change the response the user already gets.
    /// </summary>
    Task CaptureAsync(
        Exception exception,
        string? path = null,
        string? method = null,
        int? statusCode = null,
        string source = "api",
        CancellationToken ct = default);

    /// <summary>Records a client-side failure reported by the browser.</summary>
    Task CaptureClientAsync(
        string message,
        string? stack,
        string? path,
        CancellationToken ct = default);
}
