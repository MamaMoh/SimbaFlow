using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Commands;

public record UpdateUserCommand(
    Guid Id,
    string FirstName,
    string LastName,
    string? MiddleName,
    string Email,
    string? PhoneNumber,
    Guid? DepartmentId,
    bool IsSuperAdmin) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

public class UpdateUserValidator : AbstractValidator<UpdateUserCommand>
{
    public UpdateUserValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
    }
}

public class UpdateUserHandler : IRequestHandler<UpdateUserCommand, Result<Guid>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICurrentUserService _currentUser;

    public UpdateUserHandler(UserManager<ApplicationUser> userManager, ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
            return Result<Guid>.Failure("User not found", 404);

        // SECURITY: tenant admins may only modify users within their own tenant.
        if (!UserAccessGuard.CanManage(_currentUser, user))
            return Result<Guid>.Failure("User not found", 404);

        // Check email uniqueness (exclude self)
        var emailOwner = await _userManager.FindByEmailAsync(request.Email);
        if (emailOwner is not null && emailOwner.Id != user.Id)
            return Result<Guid>.Failure("Email already in use", 409);

        user.FirstName = request.FirstName;
        user.LastName = request.LastName;
        user.MiddleName = request.MiddleName;
        user.Email = request.Email;
        user.PhoneNumber = request.PhoneNumber;
        user.DepartmentId = request.DepartmentId;

        // SECURITY: only a platform SuperAdmin may change the SuperAdmin flag.
        if (_currentUser.IsSuperAdmin)
            user.IsSuperAdmin = request.IsSuperAdmin;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            return Result<Guid>.Failure(string.Join("; ", result.Errors.Select(e => e.Description)), 400);

        return Result<Guid>.Success(user.Id);
    }
}
