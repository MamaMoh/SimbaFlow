using SimbaFlow.Domain.Entities.Finance;
using SimbaFlow.Domain.Enums;

namespace SimbaFlow.API.Features.Finance;

internal static class CommissionFinanceHelpers
{
    public static void RecalcTotalsAndStatus(Commission commission)
    {
        var fees = commission.Fees?.Where(f => !f.IsDeleted).ToList() ?? [];
        var payments = commission.Payments?.Where(p => !p.IsDeleted).ToList() ?? [];
        var disputes = commission.Disputes?.Where(d => !d.IsDeleted).ToList() ?? [];

        commission.TotalFeesAmount = Math.Round(fees.Sum(f => f.AmountEtb), 2, MidpointRounding.AwayFromZero);
        commission.TotalPaidAmount = Math.Round(payments.Sum(p => p.AmountEtb), 2, MidpointRounding.AwayFromZero);
        commission.BalanceAmount = Math.Round(
            commission.TotalFeesAmount - commission.TotalPaidAmount, 2, MidpointRounding.AwayFromZero);

        if (disputes.Any(d => d.Status == DisputeStatus.Open))
        {
            commission.Status = CommissionStatus.Disputed;
            return;
        }

        if (commission.TotalFeesAmount > 0 && commission.TotalPaidAmount >= commission.TotalFeesAmount)
            commission.Status = CommissionStatus.Settled;
        else if (commission.TotalPaidAmount > 0)
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
