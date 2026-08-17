using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Departments.Commands;

public record CreateDepartmentCommand(
    string Name,
    string Code,
    string? Description,
    Guid? ParentDepartmentId,
    Guid? HeadUserId) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "office.write";
}

public class CreateDepartmentValidator : AbstractValidator<CreateDepartmentCommand>
{
    public CreateDepartmentValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Z0-9-]+$").WithMessage("Code must be uppercase alphanumeric with hyphens");
    }
}

public class CreateDepartmentHandler : IRequestHandler<CreateDepartmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateDepartmentHandler(IApplicationDbContext context, ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<Result<Guid>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var tenantId = _currentUser.TenantId;

        var codeQuery = _context.Departments.Where(d => d.Code == request.Code && !d.IsDeleted);
        if (tenantId is Guid tid)
            codeQuery = codeQuery.Where(d => d.TenantId == tid);
        else if (!_currentUser.IsSuperAdmin)
            return Result<Guid>.Failure("Tenant context required", 400);

        if (await codeQuery.AnyAsync(cancellationToken))
            return Result<Guid>.Failure("Department code already exists", 409);

        if (request.ParentDepartmentId.HasValue)
        {
            var parentQuery = _context.Departments
                .Where(d => d.Id == request.ParentDepartmentId.Value && !d.IsDeleted);
            if (tenantId is Guid parentTid)
                parentQuery = parentQuery.Where(d => d.TenantId == parentTid);

            if (!await parentQuery.AnyAsync(cancellationToken))
                return Result<Guid>.Failure("Parent department not found", 400);
        }

        var department = new Department
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            ParentDepartmentId = request.ParentDepartmentId,
            HeadUserId = request.HeadUserId,
            TenantId = tenantId,
            IsActive = true,
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(department.Id, 201);
    }
}
