using Microsoft.EntityFrameworkCore;
using SimbaFlow.API.Features.Auth.Commands;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.API.Features.Auth;

/// <summary>
/// Builds the profile handed to the browser when a token is issued.
///
/// It exists to keep one fact in one place: the agency a user belongs to. The tenant id lives on
/// the user row, but the *name* lives in the public schema on TenantInfo — so every caller that
/// wants to say "you are working in X" would otherwise repeat the same lookup, and any that forgot
/// would silently return no agency at all rather than fail.
/// </summary>
public static class SignedInProfile
{
    public static async Task<UserProfileDto> BuildAsync(
        ApplicationUser user,
        IReadOnlyList<string> permissions,
        IReadOnlyList<string> roles,
        IPlatformDbContext context,
        CancellationToken ct)
    {
        // Platform accounts — SuperAdmin, PlatformAdmin — have no agency by design, and the UI is
        // expected to say so rather than invent one. Everyone else has exactly one.
        var agencyName = user.TenantId is Guid tenantId
            ? await context.Tenants
                .AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync(ct)
            : null;

        return new UserProfileDto(
            user.Id,
            user.UserName!,
            user.FullName,
            user.Email!,
            user.PhoneNumber,
            user.ProfileImageUrl,
            user.IsFirstLogin,
            user.IsSuperAdmin,
            user.DepartmentId,
            user.TenantId,
            agencyName,
            permissions,
            roles);
    }
}
