using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Middleware that resolves the current user's StaffProfile context from the JWT claims.
/// 
/// Flow:
/// 1. Extract UserId and StaffProfileId from JWT claims
/// 2. Check memory cache for resolved context (TTL: 5 minutes)
/// 3. On cache miss: query database for active affiliations
/// 4. Populate the scoped IStaffContext for downstream CQRS handlers
/// 
/// In production, replace IMemoryCache with Redis for multi-instance deployments.
/// Invalidation: domain events (StaffTerminated, PrivilegesAssigned) should evict the cache key.
/// </summary>
public class StaffContextMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<StaffContextMiddleware> _logger;

    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public StaffContextMiddleware(RequestDelegate next, ILogger<StaffContextMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        StaffContext staffContext,
        IApplicationDbContext dbContext,
        IMemoryCache cache)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                             ?? context.User.FindFirst("sub")?.Value;

            if (Guid.TryParse(userIdClaim, out var userId))
            {
                staffContext.UserId = userId;

                // Resolve permissions and SuperAdmin from JWT claims
                staffContext.IsSuperAdmin = context.User.IsInRole("SuperAdmin")
                    || context.User.HasClaim("role", "SuperAdmin");

                var permissions = context.User.FindAll("permission")
                    .Select(c => c.Value)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                staffContext.Permissions = permissions;

                // Check for impersonation claims
                var impersonatedByClaim = context.User.FindFirst("impersonated_by")?.Value;
                if (Guid.TryParse(impersonatedByClaim, out var impersonatedByUserId))
                {
                    staffContext.IsImpersonated = true;
                    staffContext.ImpersonatedByUserId = impersonatedByUserId;
                }

                // Try to resolve staff profile from JWT claim first (avoids extra DB hit)
                var staffProfileIdClaim = context.User.FindFirst("staff_profile_id")?.Value;
                if (Guid.TryParse(staffProfileIdClaim, out var staffProfileId))
                {
                    await ResolveStaffContextAsync(staffContext, staffProfileId, dbContext, cache);
                }
                else
                {
                    // Fallback: look up staff profile by userId (for tokens issued before claim was added)
                    var cacheKey = $"staff_context_user:{userId}";
                    if (!cache.TryGetValue(cacheKey, out Guid? resolvedStaffProfileId))
                    {
                        resolvedStaffProfileId = await dbContext.StaffProfiles
                            .Where(s => s.UserId == userId)
                            .Select(s => (Guid?)s.Id)
                            .FirstOrDefaultAsync();

                        cache.Set(cacheKey, resolvedStaffProfileId, CacheTtl);
                    }

                    if (resolvedStaffProfileId.HasValue)
                    {
                        await ResolveStaffContextAsync(staffContext, resolvedStaffProfileId.Value, dbContext, cache);
                    }
                }
            }
        }

        await _next(context);
    }

    private async Task ResolveStaffContextAsync(
        StaffContext staffContext,
        Guid staffProfileId,
        IApplicationDbContext dbContext,
        IMemoryCache cache)
    {
        var cacheKey = $"staff_context:{staffProfileId}";

        if (cache.TryGetValue(cacheKey, out StaffContextCacheEntry? cached) && cached is not null)
        {
            ApplyFromCache(staffContext, cached);
            return;
        }

        // Cache miss — query database
        try
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var profile = await dbContext.StaffProfiles
                .AsNoTracking()
                .Where(s => s.Id == staffProfileId)
                .Select(s => new
                {
                    s.Id,
                    s.EmploymentStatus,
                    s.RequiresCoSignature,
                })
                .FirstOrDefaultAsync();

            if (profile is null) return;

            var affiliations = await dbContext.StaffDepartmentAffiliations
                .AsNoTracking()
                .Where(a => a.StaffProfileId == staffProfileId
                            && !a.IsDeleted
                            && a.EffectiveStartDate <= today
                            && (a.EffectiveEndDate == null || a.EffectiveEndDate >= today))
                .Join(dbContext.Departments,
                    a => a.DepartmentId,
                    d => d.Id,
                    (a, d) => new StaffAffiliationContext(
                        a.Id,
                        a.DepartmentId,
                        d.Name,
                        a.ClinicalRole,
                        a.IsPrimary,
                        a.EffectiveStartDate,
                        a.EffectiveEndDate))
                .ToListAsync();

            var entry = new StaffContextCacheEntry(
                staffProfileId,
                profile.EmploymentStatus,
                profile.RequiresCoSignature,
                affiliations);

            cache.Set(cacheKey, entry, CacheTtl);
            ApplyFromCache(staffContext, entry);
        }
        catch (Exception ex)
        {
            // Staff context resolution failure should not block the request
            _logger.LogError(ex, "Failed to resolve staff context for StaffProfileId {StaffProfileId}", staffProfileId);
        }
    }

    private static void ApplyFromCache(StaffContext staffContext, StaffContextCacheEntry entry)
    {
        staffContext.StaffProfileId = entry.StaffProfileId;
        staffContext.EmploymentStatus = entry.EmploymentStatus;
        staffContext.RequiresCoSignature = entry.RequiresCoSignature;
        staffContext.ActiveAffiliations = entry.Affiliations;
    }
}

/// <summary>
/// Internal cache entry for staff context. Immutable once created.
/// </summary>
internal record StaffContextCacheEntry(
    Guid StaffProfileId,
    EmploymentStatus EmploymentStatus,
    bool RequiresCoSignature,
    IReadOnlyList<StaffAffiliationContext> Affiliations);
