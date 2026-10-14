using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// One box on somebody else's form that this agency always answers the same way.
///
/// The visa application asks for the nearest Saudi mission, the validity, the duration of stay,
/// the transport mode and the purpose. None of that is about the candidate — it is the same on
/// every application the agency has ever filed — so it is held once here rather than forty
/// thousand times on the candidates.
///
/// Keyed by the form and the label printed on it rather than by a column per answer, which is
/// the point: a portal we map next year brings its own standing answers and needs no migration
/// to store them. The maps in the frontend ship a sensible default for each; a row here is an
/// agency saying otherwise.
///
/// One row per tenant schema per box, and the schema is already per-agency, so there is no
/// TenantId.
/// </summary>
public class AgencyPortalAnswer : BaseEntity
{
    /// <summary>The map this belongs to, e.g. "visa-application".</summary>
    public string SiteId { get; set; } = string.Empty;

    /// <summary>The label as the portal prints it, e.g. "Nearest Saudi Mission".</summary>
    public string Label { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
