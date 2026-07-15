namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Resolves the current authenticated user's context from the HTTP request.
/// </summary>
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    string? Email { get; }
    Guid? ActiveLocationId { get; }
    Guid? DepartmentId { get; }
    Guid? TenantId { get; }
    IReadOnlyList<string> Permissions { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsSuperAdmin { get; }
    bool HasPermission(string permission);
    string? IpAddress { get; }
    string? UserAgent { get; }
}
