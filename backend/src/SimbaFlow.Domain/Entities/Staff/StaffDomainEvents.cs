using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Staff;

/// <summary>
/// Raised when a staff member is terminated.
/// Handlers should:
/// - Revoke all active tokens/sessions (via ApplicationUser)
/// - Reassign open tasks/candidates to department head
/// - Notify department heads and credentialing office
/// - Update external systems (scheduling, billing, credentialing)
/// </summary>
public record StaffTerminatedEvent(
    Guid StaffProfileId,
    Guid? UserId,
    EmploymentStatus PreviousStatus,
    string? TerminatedBy,
    string? Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when a staff member is suspended pending investigation.
/// Similar to termination but reversible. Handlers should:
/// - Block login (set IsActive = false on ApplicationUser)
/// - Freeze clinical privileges
/// - Notify compliance officer
/// </summary>
public record StaffSuspendedEvent(
    Guid StaffProfileId,
    Guid? UserId,
    string? SuspendedBy,
    string? Reason) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when a new staff profile is drafted by HR (before IT account provisioning).
/// </summary>
public record StaffProfileDraftedEvent(
    Guid StaffProfileId,
    StaffType StaffType,
    string StaffId) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when an IT account is provisioned and linked to an existing staff profile.
/// </summary>
public record UserAccountProvisionedEvent(
    Guid UserId,
    Guid StaffProfileId,
    string Username) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when clinical privileges are assigned to a staff member
/// (department affiliation + clinical role + effective dates).
/// </summary>
public record ClinicalPrivilegesAssignedEvent(
    Guid StaffProfileId,
    Guid DepartmentId,
    string ClinicalRole,
    DateOnly EffectiveStartDate) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when a staff identifier (NPI, DEA, license) is verified by credentialing.
/// </summary>
public record StaffIdentifierVerifiedEvent(
    Guid StaffProfileId,
    Guid StaffIdentifierId,
    IdentifierType IdentifierType,
    string VerifiedBy) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}

/// <summary>
/// Raised when a staff identifier is approaching expiration (configurable threshold, e.g. 30 days).
/// Drives automated renewal workflow notifications.
/// </summary>
public record StaffIdentifierExpiringEvent(
    Guid StaffProfileId,
    Guid StaffIdentifierId,
    IdentifierType IdentifierType,
    DateOnly ExpirationDate,
    int DaysUntilExpiration) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
    public DateTime OccurredAt { get; } = DateTime.UtcNow;
}
