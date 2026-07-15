using MediatR;
using SimbaFlow.Application.Common.Exceptions;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces permission-based authorization.
/// Commands/queries implementing IRequirePermission are checked before handler execution.
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

        return await next(cancellationToken);
    }
}
