using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Persistence.Seeds;

/// <summary>
/// Seeds default departments/divisions for the labour export agency.
/// </summary>
public static class DepartmentSeeder
{
    public static async Task SeedDepartmentsAsync(IServiceProvider serviceProvider)
    {
        var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = serviceProvider.GetRequiredService<ILogger<ApplicationDbContext>>();

        if (await context.Departments.AnyAsync())
            return;

        var departments = new (string Code, string Name, string Description, string? ParentCode)[]
        {
            ("MGMT", "Management", "Agency leadership and administration", null),
            ("OPS", "Operations", "Candidate processing and workflow management", null),
            ("EMBASSY", "Embassy Processing", "Embassy, medical, and Tasheer clearances", "OPS"),
            ("LMIS", "LMIS & Government", "Government labour registration and compliance", "OPS"),
            ("TRAVEL", "Travel & Logistics", "Flight booking, departure, and arrival management", "OPS"),
            ("FIN", "Finance", "Commission tracking, accounting, and payments", null),
            ("HR", "Human Resources", "Staff management and recruitment", null),
            ("IT", "Information Technology", "Systems and platform support", null),
            ("FIELD", "Field Operations", "Field agents and candidate liaison", "OPS"),
        };

        var created = new Dictionary<string, Department>();
        foreach (var (code, name, desc, parentCode) in departments)
        {
            var dept = new Department
            {
                Code = code,
                Name = name,
                Description = desc,
                ParentDepartmentId = parentCode is not null && created.ContainsKey(parentCode)
                    ? created[parentCode].Id
                    : null,
                IsActive = true,
            };
            context.Departments.Add(dept);
            created[code] = dept;
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} departments for labour export agency", departments.Length);
    }
}
