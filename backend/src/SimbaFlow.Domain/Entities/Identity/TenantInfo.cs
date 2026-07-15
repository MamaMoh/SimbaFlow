using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Entities.Tenancy;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Identity;

/// <summary>
/// Represents a tenant (labour export agency) in the multi-tenant system.
/// Each tenant has its own PostgreSQL schema for data isolation.
/// Stored in the public schema for cross-tenant resolution.
/// </summary>
public class TenantInfo : BaseEntity
{
    /// <summary>Display name of the agency.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>URL-safe slug (lowercase, alphanumeric + hyphens, max 50 chars).</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>PostgreSQL schema name (derived: tenant_{slug_with_underscores}). Immutable after creation.</summary>
    public string SchemaName { get; set; } = string.Empty;

    /// <summary>Primary contact email for the agency.</summary>
    public string ContactEmail { get; set; } = string.Empty;

    /// <summary>Primary contact phone.</summary>
    public string? ContactPhone { get; set; }

    /// <summary>Physical address of the agency headquarters.</summary>
    public string? Address { get; set; }

    /// <summary>City of headquarters.</summary>
    public string? City { get; set; }

    /// <summary>Country of headquarters.</summary>
    public string? Country { get; set; }

    /// <summary>Current subscription/activation status.</summary>
    public TenantStatus SubscriptionStatus { get; set; } = TenantStatus.Active;

    /// <summary>Maximum number of user accounts allowed for this tenant.</summary>
    public int MaxUsers { get; set; } = 50;

    /// <summary>When the tenant was provisioned (schema created).</summary>
    public DateTime ProvisionedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Who provisioned the tenant (system admin username).</summary>
    public string? ProvisionedBy { get; set; }

    /// <summary>Per-agency configuration stored as JSONB.</summary>
    public TenantSettings Settings { get; set; } = new();
}
