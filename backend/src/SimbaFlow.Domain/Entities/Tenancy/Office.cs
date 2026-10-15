using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// A branch of the agency: the head office in Addis and the regional offices that register
/// candidates on its behalf.
///
/// The Office comparison report used to group by the foreign partner's name, which answers a
/// different question (who they were sent to, not who signed them up). A candidate now carries
/// the office that registered them, and the report groups on that.
///
/// One row per tenant schema per office, and the schema is already per-agency, so there is no
/// TenantId.
/// </summary>
public class Office : BaseEntity
{
    public string Name { get; set; } = string.Empty;

    /// <summary>Town or region the office is in, shown beside the name.</summary>
    public string? City { get; set; }

    public string? Phone { get; set; }

    /// <summary>
    /// An office that has closed. It keeps its candidates and its line in the reports, but is
    /// no longer offered when registering someone new.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
