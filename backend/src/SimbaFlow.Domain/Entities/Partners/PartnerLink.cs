using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Partners;

/// <summary>
/// ትስስር — links a tenant (Ethiopian agency) to a catalog PartnerAgency.
/// Stored in public schema so Art. 40 cross-tenant caps can be enforced.
/// </summary>
public class PartnerLink : BaseEntity
{
    public Guid TenantId { get; set; }

    public Guid PartnerAgencyId { get; set; }

    public PartnerAgency? PartnerAgency { get; set; }

    public DateOnly AgreementStart { get; set; }

    public DateOnly AgreementEnd { get; set; }

    public PartnerLinkStatus Status { get; set; } = PartnerLinkStatus.Active;
}
