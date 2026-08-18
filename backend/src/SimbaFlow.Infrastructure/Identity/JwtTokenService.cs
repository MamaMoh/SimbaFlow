using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Identity;

namespace SimbaFlow.Infrastructure.Identity;

/// <summary>
/// Generates short-lived JWT access tokens (15 min) with user claims, permissions, and roles.
/// Includes StaffProfileId for clinical context resolution (avoids JWT bloat).
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _configuration;
    private readonly IApplicationDbContext _context;

    public JwtTokenService(IConfiguration configuration, IApplicationDbContext context)
    {
        _configuration = configuration;
        _context = context;
    }

    public string GenerateAccessToken(ApplicationUser user, IReadOnlyList<string> permissions, IReadOnlyList<string> roles)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.Parse(
            _configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new(ClaimTypes.Email, user.Email ?? string.Empty),
            new("full_name", user.FullName),
        };

        // Staff profile context (for clinical context resolution in middleware)
        var staffProfileId = _context.StaffProfiles
            .Where(s => s.UserId == user.Id)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefault();

        if (staffProfileId.HasValue)
            claims.Add(new Claim("staff_profile_id", staffProfileId.Value.ToString()));

        // Department context
        if (user.DepartmentId.HasValue)
            claims.Add(new Claim("department_id", user.DepartmentId.Value.ToString()));

        // Tenant (future-ready)
        if (user.TenantId.HasValue)
            claims.Add(new Claim("tenant_id", user.TenantId.Value.ToString()));

        // SuperAdmin flag
        if (user.IsSuperAdmin)
            claims.Add(new Claim("role", "SuperAdmin"));

        // Roles
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        // Permissions
        foreach (var permission in permissions)
            claims.Add(new Claim("permission", permission));

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateMfaSetupToken(ApplicationUser user, int expiryMinutes)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Deliberately minimal: identity only, no permissions/roles/tenant/SuperAdmin.
        // Permission-gated endpoints will 403; only the auth-only MFA enrollment
        // endpoints (which need just the user id) accept this token.
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName ?? string.Empty),
            new("token_use", "mfa_setup"),
        };

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(Math.Clamp(expiryMinutes, 1, 60)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
