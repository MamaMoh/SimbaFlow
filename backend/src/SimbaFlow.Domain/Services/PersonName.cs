namespace SimbaFlow.Domain.Services;

/// <summary>A name in the three parts a form asks for.</summary>
public readonly record struct NameParts(string First, string Second, string Last);

/// <summary>
/// Splitting a person's name into the first, second and last a form asks for.
///
/// Ethiopian names are a given name, the father's name and the grandfather's name, so the three
/// boxes line up with how the name is actually built — but only if the right words go in the
/// right boxes, and the record does not always hold them separately. A candidate registered with
/// "ALMAZ DEBELA" in one box and "KEBEDE" in another has their father's name in the first box,
/// and a sheet built from the stored fields puts it there too.
///
/// So the parts are taken from the whole name rather than from whichever fields happen to be
/// filled. The last word is the last name, the first is the first name, and anything between is
/// the second — which for three words is exactly the three names, and for four keeps the extra
/// in the middle rather than inventing a fourth box or dropping it.
///
/// A two-word name leaves the second blank, which is what the form expects: it is the only one of
/// the three not marked required.
/// </summary>
public static class PersonName
{
    public static NameParts Split(string? fullName)
    {
        var words = (fullName ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return words.Length switch
        {
            0 => new NameParts("", "", ""),
            // One word is a first name. Putting it in the last-name box instead would be a guess
            // against the grain of every form that marks both required.
            1 => new NameParts(words[0], "", ""),
            2 => new NameParts(words[0], "", words[1]),
            _ => new NameParts(words[0], string.Join(" ", words[1..^1]), words[^1]),
        };
    }
}
