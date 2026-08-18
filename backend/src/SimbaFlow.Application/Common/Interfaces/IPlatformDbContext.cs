using Microsoft.EntityFrameworkCore;
using SimbaFlow.Domain.Entities.Identity;
using SimbaFlow.Domain.Entities.Partners;
using SimbaFlow.Domain.Entities.Staff;
using SimbaFlow.Domain.Entities.Tenancy;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Interface for the Platform DbContext (public schema).
/// Contains Identity, Tenants, Permissions, Audit, Staff, Partner catalog.
/// </summary>
public interface IPlatformDbContext
{
    DbSet<ApplicationUser> ApplicationUsers { get; }
    DbSet<ApplicationRole> ApplicationRoles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<Department> Departments { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<PasswordHistory> PasswordHistories { get; }
    DbSet<TenantInfo> Tenants { get; }
    DbSet<SystemConfiguration> SystemConfigurations { get; }
    DbSet<BotRegistrationChallenge> BotRegistrationChallenges { get; }
    DbSet<NotificationDelivery> NotificationDeliveries { get; }
    DbSet<ExchangeRate> ExchangeRates { get; }
    DbSet<PartnerAgency> PartnerAgencies { get; }
    DbSet<PartnerLink> PartnerLinks { get; }
    DbSet<PartnerAgreementDocument> PartnerAgreementDocuments { get; }
    DbSet<StaffProfile> StaffProfiles { get; }
    DbSet<StaffIdentifier> StaffIdentifiers { get; }
    DbSet<StaffDepartmentAffiliation> StaffDepartmentAffiliations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
