using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Services;

public sealed class HqOfficeSeedService : IHqOfficeSeedService
{
    public const string HqCode = "HQ";
    public const string HqName = "Head Office";

    private readonly IPlatformDbContext _context;
    private readonly ILogger<HqOfficeSeedService> _logger;

    public HqOfficeSeedService(IPlatformDbContext context, ILogger<HqOfficeSeedService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task EnsureDefaultHqOfficeAsync(
        Guid tenantId,
        string? address,
        string? city,
        string? country,
        CancellationToken ct = default)
    {
        var hasOffice = await _context.Departments
            .AnyAsync(d => d.TenantId == tenantId && !d.IsDeleted, ct);
        if (hasOffice)
            return;

        var codeTaken = await _context.Departments
            .AnyAsync(d => d.TenantId == tenantId && d.Code == HqCode && !d.IsDeleted, ct);
        if (codeTaken)
            return;

        var description = string.Join(
            ", ",
            new[] { address, city, country }.Where(s => !string.IsNullOrWhiteSpace(s)));

        _context.Departments.Add(new Department
        {
            Name = HqName,
            Code = HqCode,
            Description = string.IsNullOrWhiteSpace(description)
                ? "Default HQ branch (auto-created on provision)"
                : description,
            TenantId = tenantId,
            IsActive = true,
        });

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Seeded HQ office for tenant {TenantId}", tenantId);
    }
}
