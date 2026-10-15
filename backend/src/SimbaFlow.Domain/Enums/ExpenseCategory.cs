namespace SimbaFlow.Domain.Enums;

/// <summary>
/// What the agency spent money on. The three payouts settle money collected on someone else's
/// behalf (they reduce a payable, not profit); the rest are the agency's own costs.
/// </summary>
public enum ExpenseCategory
{
    MedicalPayout = 0,
    TicketPayout = 1,
    PartnerPayout = 2,
    Salaries = 10,
    RentAndUtilities = 11,
    TransportAndTravel = 12,
    GovernmentFees = 13,
    Marketing = 14,
    OfficeSupplies = 15,
    Other = 99
}
