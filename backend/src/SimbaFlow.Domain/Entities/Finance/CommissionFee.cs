using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

public class CommissionFee : BaseEntity
{
    public Guid CommissionId { get; set; }
    public FeeType FeeType { get; set; }
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ETB";
    public decimal AmountEtb { get; set; }
    public int SortOrder { get; set; }

    public Commission? Commission { get; set; }
}
