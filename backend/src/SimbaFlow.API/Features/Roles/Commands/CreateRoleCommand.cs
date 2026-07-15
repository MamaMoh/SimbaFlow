using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Roles.Commands;

public record CreateRoleCommand(string Name, string? Description, List<Guid> PermissionIds)
    : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "role.write";
}

public class CreateRoleValidator : AbstractValidator<CreateRoleCommand>
{
    public CreateRoleValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.PermissionIds).NotNull();
    }
}

public class CreateRoleHandler : IRequestHandler<CreateRoleCommand, Result<Guid>>
{
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IApplicationDbContext _context;

    public CreateRoleHandler(RoleManager<ApplicationRole> roleManager, IApplicationDbContext context)
    {
        _roleManager = roleManager;
        _context = context;
    }

    public async Task<Result<Guid>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        if (await _roleManager.RoleExistsAsync(request.Name))
            return Result<Guid>.Failure("Role name already exists", 409);

        var role = new ApplicationRole
        {
            Name = request.Name,
            Description = request.Description,
        };

        var createResult = await _roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
            return Result<Guid>.Failure(string.Join("; ", createResult.Errors.Select(e => e.Description)), 400);

        // Assign permissions
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
        return Result<Guid>.Success(role.Id, 201);
    }
}
