namespace SimbaFlow.Domain.Services;

/// <summary>
/// The day a candidate's contract was signed.
///
/// This used to be a date box on the registration form, sitting two fields away from a Contract
/// date box that means the same thing — so one of them was filled in and the other was not, and
/// which one varied by whoever was at the desk. Nothing downstream could then be sure which to
/// read, and the CV and the contract disagreed about the same placement.
///
/// There is only one date here and it is already known. When the desk has the contract, its own
/// date is the signing date, read off the contract itself. Until then the only date anyone has
/// is the day the record was opened, which is what a placement registered and signed on the
/// same day actually means.
/// </summary>
public static class ContractSigning
{
    public static DateOnly Date(DateOnly? contractDate, DateTime registeredAt) =>
        contractDate ?? DateOnly.FromDateTime(registeredAt);
}
