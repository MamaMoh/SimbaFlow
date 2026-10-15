using SimbaFlow.Domain.Enums;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// The agency's system chart of accounts and which account each kind of money lands in.
///
/// Only the agency fee (and "other" fees) is the agency's income. Partner share, medical and
/// ticket money is collected from the candidate on someone else's behalf, so it is owed onward
/// (a liability) rather than earned. Charging fees raises what the candidate owes (receivable);
/// a payment turns that receivable into cash.
/// </summary>
public static class LedgerAccounts
{
    public const string CashBank = "1100";
    public const string Receivable = "1200";
    public const string Clearing = "2100";
    public const string PartnerSharePayable = "2200";
    public const string MedicalPayable = "2300";
    public const string TicketPayable = "2400";
    public const string CommissionRevenue = "4100";
    public const string OtherFeeRevenue = "4200";
    public const string FeeWaivers = "4900";

    public static readonly IReadOnlyList<(string Code, string Name, AccountType Type)> SystemAccounts =
    [
        (CashBank, "Cash / Bank", AccountType.Asset),
        (Receivable, "Accounts Receivable — Candidate fees", AccountType.Asset),
        (Clearing, "Clearing / Suspense", AccountType.Liability),
        (PartnerSharePayable, "Partner share payable", AccountType.Liability),
        (MedicalPayable, "Medical fees payable", AccountType.Liability),
        (TicketPayable, "Ticket fees payable", AccountType.Liability),
        (CommissionRevenue, "Commission Revenue", AccountType.Revenue),
        (OtherFeeRevenue, "Other fee revenue", AccountType.Revenue),
        (FeeWaivers, "Fee waivers and adjustments", AccountType.Revenue),
    ];

    /// <summary>The account a fee of this type is credited to when it is charged.</summary>
    public static string ForFee(FeeType type) => type switch
    {
        FeeType.AgencyFee => CommissionRevenue,
        FeeType.PartnerShare => PartnerSharePayable,
        FeeType.Medical => MedicalPayable,
        FeeType.Ticket => TicketPayable,
        _ => OtherFeeRevenue,
    };
}
