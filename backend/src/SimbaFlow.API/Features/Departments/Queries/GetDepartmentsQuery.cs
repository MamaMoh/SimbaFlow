using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Departments.Queries;

public record GetDepartmentsQuery : IRequest<Result<List<DepartmentListDto>>>, IRequirePermission
{
    public string RequiredPermission => "department.read";
}

public record DepartmentListDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    Guid? ParentDepartmentId,
    string? ParentDepartmentName,
    bool IsActive,
    int UserCount);

public class GetDepartmentsHandler : IRequestHandler<GetDepartmentsQuery, Result<List<DepartmentListDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetDepartmentsHandler(IApplicationDbContext context) => _context = context;

    public async Task<Result<List<DepartmentListDto>>> Handle(GetDepartmentsQuery request, CancellationToken cancellationToken)
    {
        var departments = await _context.Departments
            .AsNoTracking()
            .Include(d => d.ParentDepartment)
            .Include(d => d.Users)
            .OrderBy(d => d.Name)
            .Select(d => new DepartmentListDto(
                d.Id, d.Name, d.Code, d.Description,
                d.ParentDepartmentId, d.ParentDepartment != null ? d.ParentDepartment.Name : null,
                d.IsActive, d.Users.Count))
            .ToListAsync(cancellationToken);

        return Result<List<DepartmentListDto>>.Success(departments);
    }
}
