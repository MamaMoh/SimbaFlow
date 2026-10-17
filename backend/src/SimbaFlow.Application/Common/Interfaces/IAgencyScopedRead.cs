namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// A read that only means something inside an agency — its pipeline, its candidates, its
/// dashboard — and has an honest answer for a caller who is not inside one.
///
/// A platform administrator on "All agencies" carries no tenant, so the connection has no agency
/// schema to point at. Left to run, these queries reached Postgres anyway and came back as
/// <c>relation "candidates" does not exist</c>: a 500 per dashboard panel, and one on every page
/// load for the workflow config the app shell asks for. AgencyScopeBehavior answers them with
/// <see cref="WithoutAgency"/> instead, before any SQL runs.
/// </summary>
public interface IAgencyScopedRead<out TResponse>
{
    /// <summary>The answer when there is no agency to read from — empty, or not applicable.</summary>
    TResponse WithoutAgency();
}
