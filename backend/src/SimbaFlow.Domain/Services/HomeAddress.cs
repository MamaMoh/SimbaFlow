using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Domain.Services;

/// <summary>
/// Where the candidate lives, which is not where they are going.
///
/// Choosing a partner agency fills the candidate's Country in from the partner's, so a record
/// with no address at all printed "Saudi Arabia" under Address — the destination, read by
/// whoever received the CV as the applicant's home. The country is dropped when it is the
/// destination, so what prints is an address or nothing.
/// </summary>
public static class HomeAddress
{
    public static string? Format(Candidate c)
    {
        var home = string.IsNullOrWhiteSpace(c.Country)
            || string.Equals(c.Country.Trim(), c.CountryOfTravel?.Trim(), StringComparison.OrdinalIgnoreCase)
                ? null
                : c.Country;

        var joined = string.Join(", ",
            new[] { c.HouseNo, c.Woreda, c.Subcity, c.Address, c.City, c.Region, home }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
