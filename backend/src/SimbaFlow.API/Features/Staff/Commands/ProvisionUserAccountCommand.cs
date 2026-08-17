using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Staff;

namespace SimbaFlow.API.Features.Staff.Commands;

/// <summary>
/// IT provisions a system login account and links it to an existing StaffProfile.
/// This is the second step in onboarding (after HR drafts the profile).
/// Creates the ApplicationUser, assigns base roles, and establishes the 1:1 link.
/// </summary>
public record ProvisionUserAccountCommand(
    Guid StaffProfileId,
    string Username,
    string Email,
    string TemporaryPassword,
    Guid? DepartmentId,
    bool RequireMfa,
    string[]? AllowedIpRanges,
    List<string>? RoleNames) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "users.write";
}

// --- Validator ---
public class ProvisionUserAccountValidator : AbstractValidator<ProvisionUserAccountCommand>
{
    public ProvisionUserAccountValidator()
    {
        RuleFor(x => x.StaffProfileId).NotEmpty();
        RuleFor(x => x.Username).NotEmpty().MaximumLength(100)
            .Matches("^[a-zA-Z0-9._@-]+$")
            .WithMessage("Username can only contain letters, numbers, dots, hyphens, @ and underscores");
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.TemporaryPassword).NotEmpty().MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter")
            .Matches("[0-9]").WithMessage("Password must contain a digit")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a special character");
    }
}

// --- Handler ---
public class ProvisionUserAccountHandler : IRequestHandler<ProvisionUserAccountCommand, Result<Guid>>
{
    private readonly IPlatformDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;

    public ProvisionUserAccountHandler(
        IPlatformDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<Result<Guid>> Handle(ProvisionUserAccountCommand request, CancellationToken cancellationToken)
    {
        // Verify staff profile exists and doesn't already have an account
        var staffProfile = await _context.StaffProfiles
            .FirstOrDefaultAsync(s => s.Id == request.StaffProfileId, cancellationToken);

        if (staffProfile is null)
            return Result<Guid>.Failure("Staff profile not found", 404);

        if (staffProfile.UserId.HasValue)
            return Result<Guid>.Failure("Staff profile already has a linked user account", 409);

        // Check username uniqueness
        var existingUser = await _userManager.FindByNameAsync(request.Username);
        if (existingUser is not null)
            return Result<Guid>.Failure("Username already exists", 409);

        // Check email uniqueness
        var existingEmail = await _userManager.FindByEmailAsync(request.Email);
        if (existingEmail is not null)
            return Result<Guid>.Failure("Email already exists", 409);

        // Create the ApplicationUser
        var user = new ApplicationUser
        {
            UserName = request.Username,
            Email = request.Email,
            FirstName = staffProfile.FirstName,
            LastName = staffProfile.LastName,
            MiddleName = staffProfile.MiddleName,
            DepartmentId = request.DepartmentId,
            RequireMfa = request.RequireMfa,
            AllowedIpRanges = request.AllowedIpRanges,
            IsFirstLogin = true,
            MustChangePassword = true,
            IsActive = true,
            PasswordExpiresAt = DateTime.UtcNow.AddDays(90),
        };

        var createResult = await _userManager.CreateAsync(user, request.TemporaryPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result<Guid>.Failure(errors, 400);
        }

        // Link to staff profile
        staffProfile.UserId = user.Id;

        // Assign roles
        if (request.RoleNames?.Count > 0)
        {
            var validRoles = new List<string>();
            foreach (var roleName in request.RoleNames)
            {
                if (await _roleManager.RoleExistsAsync(roleName))
                    validRoles.Add(roleName);
            }

            if (validRoles.Count > 0)
                await _userManager.AddToRolesAsync(user, validRoles);
        }

        // Raise domain event
        staffProfile.AddDomainEvent(new UserAccountProvisionedEvent(
            user.Id, staffProfile.Id, request.Username));

        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(user.Id, 201);
    }
}
