using MediatR;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces permission-based authorization.
/// Commands/queries implementing IRequirePermission (one permission) or IRequireAnyPermission
/// (any one of several) are checked before handler execution.
/// SuperAdmin bypasses those checks — but not IRequirePlatformAdmin or IRequireAgencyOwner,
/// the two gates a permission grant cannot open.
/// </summary>
public class AuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;

    public AuthorizationBehavior(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // First, and with no bypass: the platform's own work is not reachable by granting
        // yourself a permission.
        if (request is IRequirePlatformAdmin)
        {
            if (string.IsNullOrEmpty(_currentUser.UserId))
                throw new UnauthorizedAccessException("Authentication required");

            if (!_currentUser.IsSuperAdmin)
                throw new ForbiddenAccessException(
                    "Roles and permissions are managed by the platform team.");
        }

        // The platform team as a whole, support desk included. Still a role, never a permission.
        if (request is IRequirePlatformStaff)
        {
            if (string.IsNullOrEmpty(_currentUser.UserId))
                throw new UnauthorizedAccessException("Authentication required");

            if (!_currentUser.IsSuperAdmin && !PlatformRoles.IsStaff(_currentUser.Roles))
                throw new ForbiddenAccessException("This is for the platform team.");
        }

        // Then the agency's own shape — who it is, how its pipeline runs. Its owner, or the
        // platform team, and no permission grant reaches it either.
        if (request is IRequireAgencyOwner ownerRequest)
        {
            if (string.IsNullOrEmpty(_currentUser.UserId))
                throw new UnauthorizedAccessException("Authentication required");

            if (!_currentUser.IsSuperAdmin && !AgencyRoles.Owns(_currentUser.Roles))
                throw new ForbiddenAccessException(ownerRequest.OwnerOnlyMessage);
        }

        if (request is IRequirePermission permissionRequest)
        {
            // SuperAdmin bypasses all permission checks
            if (_currentUser.IsSuperAdmin)
                return await next(cancellationToken);

            var requiredPermission = permissionRequest.RequiredPermission;

            if (string.IsNullOrEmpty(_currentUser.UserId))
                throw new UnauthorizedAccessException("Authentication required");

            if (!_currentUser.HasPermission(requiredPermission))
                throw new ForbiddenAccessException(
                    $"Permission '{requiredPermission}' is required to perform this action");
        }

        if (request is IRequireAnyPermission anyPermissionRequest)
        {
            if (_currentUser.IsSuperAdmin)
                return await next(cancellationToken);

            if (string.IsNullOrEmpty(_currentUser.UserId))
                throw new UnauthorizedAccessException("Authentication required");

            var accepted = anyPermissionRequest.AcceptedPermissions;

            // An empty list would admit everyone, which is how an action ends up with no gate at
            // all while looking like it has one.
            if (accepted.Count == 0 || !accepted.Any(_currentUser.HasPermission))
                throw new ForbiddenAccessException(
                    $"One of these permissions is required to perform this action: {string.Join(", ", accepted)}");
        }

        return await next(cancellationToken);
    }
}
