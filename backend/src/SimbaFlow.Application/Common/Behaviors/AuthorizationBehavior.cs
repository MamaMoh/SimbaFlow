using MediatR;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces permission-based authorization.
/// Commands/queries implementing IRequirePermission (one permission) or IRequireAnyPermission
/// (any one of several) are checked before handler execution.
/// SuperAdmin bypasses all checks.
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
