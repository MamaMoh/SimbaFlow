using SimbaFlow.Application.Common.Filtering;
using SimbaFlow.Domain.Entities.Travel;

namespace SimbaFlow.API.Features.Exceptions.Queries;

/// <summary>
/// Which columns of the exceptions board may be filtered, and against what.
///
/// Type and Status are held as enums and shown as their names, so they are matched by name —
/// typing "run" under Type finds the runaways. The chips above the table ask the same two
/// questions, and both are kept: the chips are the quick answer and the funnel is the one that
/// combines with a filter on somebody's name.
///
/// The candidate half is <see cref="Candidates.Queries.CandidateFilters.Joined"/>.
/// </summary>
public static class ExceptionFilters
{
    public static FilterableColumns<ExceptionCase> For() =>
        new FilterableColumns<ExceptionCase>()
            .Enum("type", e => e.Type)
            .Enum("status", e => e.Status)
            .Timestamp("openedAt", e => e.OpenedAt)
            .Money("impact", e => e.FinancialImpactAmount);
}
