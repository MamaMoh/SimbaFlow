namespace SimbaFlow.Application.Common.Exceptions;

/// <summary>
/// Thrown when an authenticated user lacks the permission required for an action.
/// Mapped to HTTP 403 (vs. UnauthorizedAccessException which maps to 401).
/// </summary>
public class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message)
    {
    }
}
