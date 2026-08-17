using SimbaFlow.Domain.Common;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Entities.Finance;

/// <summary>
/// Commission record (Unit 4 shell + Unit 5 fees/payments/disputes).
/// </summary>
public class Commission : BaseEntity
{
    public Guid CandidateId { get; set; }
    public CommissionStatus Status { get; set; } = CommissionStatus.Open;
    public string? CountryOfTravel { get; set; }
    public string? OfficeName { get; set; }
    public DateOnly? ContractDate { get; set; }
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public Guid OpenedByUserId { get; set; }

    /// <summary>Denormalized Σ fee AmountEtb.</summary>
    public decimal TotalFeesAmount { get; set; }

    /// <summary>Denormalized Σ payment AmountEtb.</summary>
    public decimal TotalPaidAmount { get; set; }

    /// <summary>TotalFeesAmount − TotalPaidAmount (ETB).</summary>
    public decimal BalanceAmount { get; set; }

    public ICollection<CommissionFee> Fees { get; set; } = [];
    public ICollection<Payment> Payments { get; set; } = [];
    public ICollection<Dispute> Disputes { get; set; } = [];
}
