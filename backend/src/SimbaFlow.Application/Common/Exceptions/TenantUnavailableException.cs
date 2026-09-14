namespace SimbaFlow.Application.Common.Exceptions;

/// <summary>
/// The signed-in user belongs to an agency that cannot be used right now — removed, suspended, or
/// never provisioned a schema.
///
/// Raised where the connection is prepared rather than left to surface later. Without it the
/// search_path simply stays on "public", and the first query to touch an agency table fails with
/// <c>relation "candidates" does not exist</c> — which reads as a broken app rather than an
/// account that needs attention, and repeats on every page the user opens.
/// </summary>
public class TenantUnavailableException : Exception
{
    public TenantUnavailableException(string message) : base(message) { }
}
