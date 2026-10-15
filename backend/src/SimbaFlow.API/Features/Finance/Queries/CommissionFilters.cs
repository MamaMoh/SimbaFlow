using SimbaFlow.Application.Common.Filtering;
using SimbaFlow.Domain.Entities.Finance;

namespace SimbaFlow.API.Features.Finance.Queries;

/// <summary>
/// Which columns of the commission board may be filtered, and against what.
///
/// Split in two because the board is as well: a row is a commission joined to the candidate it
/// is about, and the two halves are narrowed separately before they meet. The same parsed list
/// is offered to both — each ignores what it does not recognise — so a filter on the person and
/// a filter on the money apply together without either half knowing about the other.
///
/// The candidate half is <see cref="Candidates.Queries.CandidateFilters.Joined"/>.
/// </summary>
public static class CommissionFilters
{
    public static FilterableColumns<Commission> For() =>
        new FilterableColumns<Commission>()
            .Text("countryOfTravel", c => c.CountryOfTravel)
            .Text("partnerName", c => c.PartnerName)
            .Enum("status", c => c.Status)
            // Nullable only in the funnel's eyes: these are totals that are always a number,
            // and the cast is what lets one set of comparisons serve both kinds of column.
            .Money("totalFeesAmount", c => c.TotalFeesAmount)
            .Money("totalPaidAmount", c => c.TotalPaidAmount)
            .Money("balanceAmount", c => c.BalanceAmount)
            .Timestamp("openedAt", c => c.OpenedAt);
}
