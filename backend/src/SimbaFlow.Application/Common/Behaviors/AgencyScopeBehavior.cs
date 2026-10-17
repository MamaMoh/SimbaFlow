using MediatR;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Application.Common.Behaviors;

/// <summary>
/// Answers an <see cref="IAgencyScopedRead{TResponse}"/> without touching the database when the
/// caller is not inside an agency: a platform administrator with no agency chosen, or the support
/// desk, which belongs to none.
///
/// Runs after authorization, so a caller who may not read this at all is still refused rather
/// than handed an empty answer.
/// </summary>
public class AgencyScopeBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;

    public AgencyScopeBehavior(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IAgencyScopedRead<TResponse> scoped && _currentUser.TenantId is null)
            return scoped.WithoutAgency();

        return await next(cancellationToken);
    }
}
