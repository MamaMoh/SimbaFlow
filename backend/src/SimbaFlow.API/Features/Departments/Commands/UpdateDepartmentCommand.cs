using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Departments.Commands;

public record UpdateDepartmentCommand(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    Guid? ParentDepartmentId,
    Guid? HeadUserId,
    bool IsActive) : IRequest<Result<Guid>>, IRequirePermission
{
    public string RequiredPermission => "office.write";
}

public class UpdateDepartmentValidator : AbstractValidator<UpdateDepartmentCommand>
{
    public UpdateDepartmentValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Z0-9-]+$").WithMessage("Code must be uppercase alphanumeric with hyphens");
    }
}

public class UpdateDepartmentHandler : IRequestHandler<UpdateDepartmentCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;

    public UpdateDepartmentHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<Guid>> Handle(UpdateDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _context.Departments
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (department is null)
            return Result<Guid>.Failure("Department not found", 404);

        // Check code uniqueness (exclude self)
        var codeExists = await _context.Departments
            .AnyAsync(d => d.Code == request.Code && d.Id != request.Id, cancellationToken);
        if (codeExists)
            return Result<Guid>.Failure("Department code already exists", 409);

        // Prevent circular parent reference
        if (request.ParentDepartmentId == request.Id)
            return Result<Guid>.Failure("A department cannot be its own parent", 400);

        department.Name = request.Name;
        department.Code = request.Code;
        department.Description = request.Description;
        department.ParentDepartmentId = request.ParentDepartmentId;
        department.HeadUserId = request.HeadUserId;
        department.IsActive = request.IsActive;

        await _context.SaveChangesAsync(cancellationToken);
        return Result<Guid>.Success(department.Id);
    }
}
