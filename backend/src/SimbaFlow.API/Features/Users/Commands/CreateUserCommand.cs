using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Users.Commands;

// --- Command ---
public record CreateUserCommand(
    string Username,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    string? MiddleName,
    string? PhoneNumber,
    Guid? DepartmentId,
    Guid? TenantId,
    bool IsSuperAdmin,
    List<string>? RoleNames,
    string? RoleName) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

// --- Validator ---
public class CreateUserValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$").WithMessage("Username can only contain letters, numbers, dots, hyphens, and underscores");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character");
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

// --- Handler ---
public class CreateUserHandler : IRequestHandler<CreateUserCommand, Result<Guid>>
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IPlatformDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateUserHandler(
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IPlatformDbContext context,
        ICurrentUserService currentUser)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
    {
        // Check username uniqueness
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser is not null)
            return Result<Guid>.Failure("Username already exists", 409);

        // Check email uniqueness
        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
            return Result<Guid>.Failure("Email already exists", 409);

        // Work out the roles first: refusing after the account exists would leave an orphan.
        var rolesToAssign = request.RoleNames?.ToList() ?? [];
        if (!string.IsNullOrEmpty(request.RoleName) && !rolesToAssign.Contains(request.RoleName))
            rolesToAssign.Add(request.RoleName);

        // SECURITY: the IsSuperAdmin flag below is already forced false for a tenant admin, but the
        // SuperAdmin *role* is what CurrentUserService actually reads — so granting it here would
        // confer exactly the privilege the flag was protecting.
        var forbidden = UserAccessGuard.RolesCallerMayNotGrant(_currentUser, rolesToAssign);
        if (forbidden.Count > 0)
            return Result<Guid>.Failure(
                $"You cannot assign {string.Join(" or ", forbidden)} — that is a platform role.", 403);

        // SECURITY: only a platform SuperAdmin may grant SuperAdmin or place a user in an
        // arbitrary tenant. Tenant admins can only create ordinary users in their OWN tenant.
        var isSuperAdmin = request.IsSuperAdmin;
        var tenantId = request.TenantId;
        if (!_currentUser.IsSuperAdmin)
        {
            isSuperAdmin = false;
            tenantId = _currentUser.TenantId;
        }

        // Everyone belongs to an agency, except the handful of people who run the platform.
        //
        // Without this a user could be created with no agency at all, and the result was not an
        // account with reduced access but a broken one: a tenant role with no tenant resolves to no
        // schema, so the person signs in and every page they open is empty. Platform roles are the
        // exception because they are defined by having no agency.
        var isPlatformAccount = isSuperAdmin || rolesToAssign.Any(UserAccessGuard.IsPlatformRole);
        if (tenantId is null && !isPlatformAccount)
        {
            return Result<Guid>.Failure(
                "Choose an agency for this user. Only platform administrators may be created "
                + "without one.", 400);
        }

        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            MiddleName = request.MiddleName,
            PhoneNumber = request.PhoneNumber,
            DepartmentId = request.DepartmentId,
            TenantId = tenantId,
            IsSuperAdmin = isSuperAdmin,
            IsFirstLogin = true,
            MustChangePassword = true,
            IsActive = true,
            PasswordExpiresAt = DateTime.UtcNow.AddDays(90),
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<Guid>.Failure(errors, 400);
        }

        // Assign roles
        if (rolesToAssign.Count > 0)
        {
            var validRoles = new List<string>();
            foreach (var roleName in rolesToAssign)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                    validRoles.Add(roleName);
            }

            if (validRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, validRoles);
        }

        return Result<Guid>.Success(user.Id, 201);
    }
}
