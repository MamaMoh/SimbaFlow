using System.Globalization;
using System.Linq.Expressions;
using SimbaFlow.Application.Common.Filtering;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.API.Features.Candidates.Queries;

/// <summary>
/// Which columns of the candidate list may be filtered, and against what.
///
/// The field names are the table's own column ids, so the funnel under a heading and the clause
/// that ends up in the query are talking about the same thing. Anything not listed here is
/// dropped: a field name arriving from a browser never reaches a column.
///
/// Three columns on that board are deliberately absent. "Stage" and "Status" are chosen from
/// the chips above the table, which narrow the query already and would be two controls for one
/// question. "Deleted by" belongs to the inactive list and is not a column anyone sorts a
/// working list by.
/// </summary>
public static class CandidateFilters
{
    public static FilterableColumns<Candidate> For(DateOnly today) =>
        new FilterableColumns<Candidate>()
            // Stored in three columns and read as one, which is how it is typed and how it is
            // searched. The same shape the global search uses, so the two agree.
            .Custom("fullName", f => Name(f))
            .Text("passportNumber", c => c.PassportNumber)
            .Text("labourId", c => c.LabourId)
            .Text("eNumber", c => c.ENumber)
            .Text("phoneNumber", c => c.PhoneNumber)
            .Text("contactPerson2", c => c.ContactPerson2)
            .Text("occupation", c => c.Occupation)
            .Text("worksIn", c => c.WorksIn)
            .Text("countryOfTravel", c => c.CountryOfTravel)
            .Text("partnerName", c => c.PartnerName)
            .Text("sponsorName", c => c.SponsorName)
            .Text("visaNumber", c => c.VisaNumber)
            .Text("agentName", c => c.AgentName)
            .Number("experienceAbroadYears", c => c.ExperienceAbroadYears)
            .Timestamp("registeredAt", c => c.RegisteredAt)
            .Custom("age", f => Age(f, today));

    /// <summary>
    /// The whole name as well as its parts.
    ///
    /// "Almaz Kebede" is in neither FirstName nor LastName, so a filter on either finds nothing
    /// for a name typed the way it is written down. Matched against the name assembled both
    /// with and without a middle name, because most people have one recorded and the ones who
    /// do not would otherwise match a string with a double space in it.
    /// </summary>
    private static Expression<Func<Candidate, bool>>? Name(TableFilter f)
    {
        if (f.Operator is FilterOperator.Blank or FilterOperator.NotBlank) return null;
        if (string.IsNullOrWhiteSpace(f.Value)) return null;

        var needle = f.Value.Trim().ToLowerInvariant();

        return f.Operator switch
        {
            FilterOperator.Contains => c =>
                (c.FirstName + " " + (c.MiddleName ?? "") + " " + c.LastName).ToLower().Contains(needle) ||
                (c.FirstName + " " + c.LastName).ToLower().Contains(needle),
            FilterOperator.StartsWith => c =>
                (c.FirstName + " " + c.LastName).ToLower().StartsWith(needle) ||
                c.FirstName.ToLower().StartsWith(needle),
            FilterOperator.Equals => c =>
                (c.FirstName + " " + (c.MiddleName ?? "") + " " + c.LastName).ToLower() == needle ||
                (c.FirstName + " " + c.LastName).ToLower() == needle,
            FilterOperator.NotEquals => c =>
                (c.FirstName + " " + (c.MiddleName ?? "") + " " + c.LastName).ToLower() != needle &&
                (c.FirstName + " " + c.LastName).ToLower() != needle,
            _ => null,
        };
    }

    /// <summary>
    /// An age asked for in years, held as a date of birth.
    ///
    /// Turned into dates rather than computing an age per row: an age worked out for every
    /// candidate cannot use an index, and recomputing it over the whole table to find a handful
    /// is the slow way round. Somebody is 25 from their 25th birthday until the day before their
    /// 26th, which is what the two bounds say — and note they run backwards, because an older
    /// person has an earlier date of birth.
    /// </summary>
    private static Expression<Func<Candidate, bool>>? Age(TableFilter f, DateOnly today)
    {
        // Every candidate has one; it is required to register. There is no blank to ask about.
        if (f.Operator is FilterOperator.Blank) return _ => false;
        if (f.Operator is FilterOperator.NotBlank) return _ => true;

        if (!TryYears(f.Value, out var years)) return null;

        // Born on or before this day to have reached `years`; born after this one to still be
        // `years` and not older.
        var newest = today.AddYears(-years);
        var oldest = today.AddYears(-(years + 1));

        switch (f.Operator)
        {
            case FilterOperator.Equals:
                return c => c.DateOfBirth <= newest && c.DateOfBirth > oldest;
            case FilterOperator.NotEquals:
                return c => c.DateOfBirth > newest || c.DateOfBirth <= oldest;
            case FilterOperator.GreaterThan:
                return c => c.DateOfBirth <= oldest;
            case FilterOperator.GreaterOrEqual:
                return c => c.DateOfBirth <= newest;
            case FilterOperator.LessThan:
                return c => c.DateOfBirth > newest;
            case FilterOperator.LessOrEqual:
                return c => c.DateOfBirth > oldest;
            case FilterOperator.Between:
                if (!TryYears(f.Value2, out var upper)) return null;
                // Taken as a range whichever way round it was typed.
                var (low, high) = years <= upper ? (years, upper) : (upper, years);
                var youngest = today.AddYears(-low);
                var eldest = today.AddYears(-(high + 1));
                return c => c.DateOfBirth <= youngest && c.DateOfBirth > eldest;
            default:
                return null;
        }
    }

    /// <summary>A plausible age. Keeps a stray year like "1998" from asking for a date in 28 BC.</summary>
    private static bool TryYears(string? raw, out int years)
    {
        years = 0;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v))
            return false;
        if (v < 0 || v > 120) return false;
        years = v;
        return true;
    }
}
