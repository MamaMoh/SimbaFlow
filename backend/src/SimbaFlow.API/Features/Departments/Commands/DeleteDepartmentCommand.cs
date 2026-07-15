using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Departments.Commands;

public record DeleteDepartmentCommand(Guid Id) : IRequest<Result<bool>>, IRequirePermission
{
    public string RequiredPermission => "department.write";
}

public class DeleteDepartmentHandler : IRequestHandler<DeleteDepartmentCommand, Result<bool>>
{
    private readonly IApplicationDbContext _context;

    public DeleteDepartmentHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<bool>> Handle(DeleteDepartmentCommand request, CancellationToken cancellationToken)
    {
        var department = await _context.Departments
            .Include(d => d.Users)
            .Include(d => d.ChildDepartments)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (department is null)
            return Result<bool>.Failure("Department not found", 404);

        if (department.Users.Count > 0)
            return Result<bool>.Failure("Cannot delete a department with assigned users. Reassign users first.", 409);

        if (department.ChildDepartments.Count > 0)
            return Result<bool>.Failure("Cannot delete a department with child departments. Remove children first.", 409);

        _context.Departments.Remove(department);
        await _context.SaveChangesAsync(cancellationToken);

        return Result<bool>.Success(true);
    }
}
