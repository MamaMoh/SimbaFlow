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
    public string RequiredPermission => "department.write";
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

    public CreateDepartmentHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(CreateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var codeExists = await _context.Departments
            .AnyAsync(d => d.Code == request.Code, cancellationToken);
        if (codeExists)
            return Result<Guid>.Failure("Department code already exists", 409);

        if (request.ParentDepartmentId.HasValue)
        {
            var parentExists = await _context.Departments
                .AnyAsync(d => d.Id == request.ParentDepartmentId.Value, cancellationToken);
            if (!parentExists)
                return Result<Guid>.Failure("Parent department not found", 400);
        }

        var department = new Department
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
            ParentDepartmentId = request.ParentDepartmentId,
            HeadUserId = request.HeadUserId,
            IsActive = true,
        };

        _context.Departments.Add(department);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(department.Id, 201);
    }
}
