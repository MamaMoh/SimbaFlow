using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Finance;

internal static class CommissionFinanceHelpers
{
    public static void RecalcTotalsAndStatus(Commission commission)
    {
        var fees = commission.Fees?.Where(f => !f.IsDeleted).ToList() ?? [];
        var payments = commission.Payments?.Where(p => !p.IsDeleted && p.VoidedAt == null).DistinctBy(p => p.Id).ToList() ?? [];
        // By id: a dispute added to the collection by hand and again by fix-up must count once.
        var disputes = commission.Disputes?.Where(d => !d.IsDeleted).DistinctBy(d => d.Id).ToList() ?? [];

        commission.TotalFeesAmount = Math.Round(fees.Sum(f => f.AmountEtb), 2, MidpointRounding.AwayFromZero);
        commission.TotalPaidAmount = Math.Round(payments.Sum(p => p.AmountEtb), 2, MidpointRounding.AwayFromZero);
        commission.TotalAdjustedAmount = Math.Round(
            disputes.Where(d => d.Status == DisputeStatus.Resolved).Sum(d => d.AdjustmentAmountEtb),
            2, MidpointRounding.AwayFromZero);
        commission.BalanceAmount = Math.Round(
            commission.TotalFeesAmount - commission.TotalPaidAmount - commission.TotalAdjustedAmount,
            2, MidpointRounding.AwayFromZero);

        if (disputes.Any(d => d.Status == DisputeStatus.Open))
        {
            commission.Status = CommissionStatus.Disputed;
            return;
        }

        var cleared = commission.TotalPaidAmount + commission.TotalAdjustedAmount;
        if (commission.TotalFeesAmount > 0 && cleared >= commission.TotalFeesAmount)
            commission.Status = CommissionStatus.Settled;
        else if (cleared > 0)
            commission.Status = CommissionStatus.Partial;
        else
            commission.Status = CommissionStatus.Open;
    }

    public static int StatusPriority(CommissionStatus status) => status switch
    {
        CommissionStatus.Disputed => 0,
        CommissionStatus.Open => 1,
        CommissionStatus.Partial => 2,
        CommissionStatus.Settled => 3,
        _ => 9
    };
}
