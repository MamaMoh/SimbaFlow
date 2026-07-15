using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Roles.Commands;

public record UpdateRoleCommand(Guid Id, string Name, string? Description, List<Guid> PermissionIds)
    : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "role.write";
}

public class UpdateRoleValidator : AbstractValidator<UpdateRoleCommand>
{
    public UpdateRoleValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PermissionIds).NotNull();
    }
}

public class UpdateRoleHandler : IRequestHandler<UpdateRoleCommand, Result<Guid>>
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;

    public UpdateRoleHandler(RoleManager<ApplicationRole> roleManager, IApplicationDbContext context)
    {
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<Result<Guid>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
    {
        var role = await _roleManager.FindByIdAsync(request.Id.ToString());
        if (role is null)
            return Result<Guid>.Failure("Role not found", 404);

        if (role.IsSystemRole)
            return Result<Guid>.Failure("Cannot modify a system role", 403);

        // Check name uniqueness (exclude self)
        var existingRole = await _roleManager.FindByNameAsync(request.Name);
        if (existingRole is not null && existingRole.Id != role.Id)
            return Result<Guid>.Failure("Role name already exists", 409);

        role.Name = request.Name;
        role.Description = request.Description;
        role.UpdatedAt = DateTime.UtcNow;

        var updateResult = await _roleManager.UpdateAsync(role);
        if (!updateResult.Succeeded)
            return Result<Guid>.Failure(string.Join("; ", updateResult.Errors.Select(e => e.Description)), 400);

        // Replace permissions
        var existingPermissions = await _context.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        _context.RolePermissions.RemoveRange(existingPermissions);

        var validPermissions = await _context.Permissions
            .IgnoreQueryFilters()
            .Where(p => request.PermissionIds.Contains(p.Id) && !p.IsDeleted)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        foreach (var permId in validPermissions)
        {
            _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permId });
        }

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(role.Id);
    }
}
