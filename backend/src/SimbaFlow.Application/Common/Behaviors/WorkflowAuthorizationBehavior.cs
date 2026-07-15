using MediatR;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// Pipeline behavior that enforces office-level access control.
/// Checks that the current user belongs to the target office for the operation.
/// Agency Owners and System Admins bypass this check.
/// </summary>
public class WorkflowAuthorizationBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITenantContext _tenantContext;

    public WorkflowAuthorizationBehavior(ICurrentUserService currentUser, ITenantContext tenantContext)
    {
        _currentUser = currentUser;
        _tenantContext = tenantContext;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Only check if the request requires office access
        if (request is not IRequireOfficeAccess officeRequest)
            return await next();

        // System admins bypass all checks
        if (_tenantContext.IsSystemAdmin)
            return await next();

        // If no specific office is targeted, allow
        if (officeRequest.TargetOfficeId is null)
            return await next();

        // Agency Owners have access to all offices in their tenant
        if (_currentUser.Roles?.Contains("AgencyOwner") == true)
            return await next();

        // Check that user's office matches the target office
        if (_tenantContext.OfficeId != officeRequest.TargetOfficeId)
        {
            // Return a 403 forbidden result
            if (typeof(TResponse).IsGenericType &&
                typeof(TResponse).GetGenericTypeDefinition() == typeof(Result<>))
            {
                var failureMethod = typeof(TResponse).GetMethod("Failure", new[] { typeof(string), typeof(int) });
                if (failureMethod is not null)
                    return (TResponse)failureMethod.Invoke(null, ["Access denied: you do not have permission for this office.", 403])!;
            }

            throw new UnauthorizedAccessException("Access denied: you do not have permission for this office.");
        }

        return await next();
    }
}
