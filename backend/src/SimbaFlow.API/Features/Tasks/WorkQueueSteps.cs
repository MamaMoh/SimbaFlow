using System.Globalization;

namespace SimbaFlow.API.Features.Tasks;

/// <summary>
/// What a candidate on one board is waiting for, in the words the desk would use.
///
/// A step either needs the person at that desk ("Book medical") or is waiting on somebody
/// else: a clinic appointment next week, the embassy deciding on a submitted visa, a flight
/// that has not left yet. My work splits the two, so the first list is what to do today and
/// the second is what to keep an eye on.
///
/// Read from the candidate's status bag only, the same values the boards themselves show, so
/// a row here and the row on the board cannot disagree about where somebody is.
/// </summary>
public static class WorkQueueSteps
{
    public record Step(string Text, bool NeedsAction);

    private static Step Act(string text) => new(text, true);
    private static Step Wait(string text) => new(text, false);

    public static Step Intake() => Act("Review and send to New Contracts");

    public static Step NewContracts(IReadOnlyDictionary<string, string> status) =>
        Is(Value(status, "status"), "Ready")
            ? Act("Send to Embassy")
            : Act("Review contract and mark Ready");

    /// <summary>
    /// Embassy runs medical and Tasheer side by side and then the visa, so a candidate can owe
    /// the desk two things at once. Everything the desk owes is listed, joined; only when it
    /// owes nothing does the row read as waiting.
    /// </summary>
    public static Step Embassy(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        var todo = new List<string>();
        var waiting = new List<string>();

        var medical = Value(status, "medical");
        if (string.IsNullOrEmpty(medical) || Is(medical, "Pending"))
            todo.Add("Book medical");
        else if (Is(medical, "Unfit"))
            todo.Add("Medical unfit: rebook or remove");
        else if (Is(medical, "Booked"))
            Appointment(Date(status, "medical_appointment_date"), today, "medical", todo, waiting);

        var tasheer = Value(status, "tasheer");
        if (string.IsNullOrEmpty(tasheer) || Is(tasheer, "Pending"))
            todo.Add("Book Tasheer");
        else if (Is(tasheer, "Expired"))
            todo.Add("Tasheer expired: rebook");
        else if (Is(tasheer, "Booked"))
            Appointment(Date(status, "tasheer_appointment_date"), today, "Tasheer", todo, waiting);

        var visa = Value(status, "visa");
        var cleared = Is(medical, "Fit") && Is(tasheer, "Book Done");
        if (cleared && string.IsNullOrEmpty(visa))
            todo.Add("Mark visa Ready");
        else if (Is(visa, "Ready"))
            waiting.Add("Visa with Case Executive");
        else if (Is(visa, "Submitted"))
            waiting.Add("Visa submitted, waiting for result");
        else if (Is(visa, "Rejected"))
            todo.Add("Visa rejected: resubmit");
        else if (Is(visa, "Issued"))
            todo.Add("Send to LMIS");

        if (todo.Count > 0)
            return Act(string.Join(" · ", todo));
        return Wait(waiting.Count > 0 ? string.Join(" · ", waiting) : "Waiting for clearances");
    }

    public static Step CaseExecutive(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        var visa = Value(status, "visa");
        if (Is(visa, "Submitted"))
        {
            var since = Date(status, "visa_submission_date");
            return Wait(since is null
                ? "Submitted, waiting for the embassy"
                : $"Submitted {Ago(since.Value, today)}, waiting for the embassy");
        }
        return Act("Submit visa documents");
    }

    /// <summary>
    /// Insurance comes first: the milestones refuse to move until it is Available. After the
    /// upload the agency is waiting on the ministry, so Uploaded and Check Verified read as
    /// waiting rather than as something the desk can push.
    /// </summary>
    public static Step Lmis(IReadOnlyDictionary<string, string> status)
    {
        var insurance = Value(status, "insurance");
        if (!Is(insurance, "Available"))
            return Act(Is(insurance, "Insurance Paid") ? "Confirm insurance is available" : "Pay insurance");

        var milestone = Value(status, "milestone");
        if (string.IsNullOrEmpty(milestone))
            return Act("Upload to LMIS");
        if (Is(milestone, "Uploaded"))
            return Wait("Uploaded, waiting for the LMIS check");
        if (Is(milestone, "Check Verified"))
            return Wait("Verified, waiting for LMIS to issue");
        return Act("Send to Ticket");
    }

    public static Step Ticket(IReadOnlyDictionary<string, string> status)
    {
        if (IsTrue(Value(status, "canceled")))
            return Act("Ticket canceled: rebook");
        if (!Is(Value(status, "ticket_status"), "Booking Complete"))
            return Act("Book ticket");
        if (string.IsNullOrEmpty(Value(status, "destination")) || Date(status, "flight_date") is null)
            return Act("Add destination and flight date");
        return Act("Send to Departure");
    }

    public static Step Departure(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        if (IsTrue(Value(status, "canceled")))
            return Act("Ticket canceled: send back to Ticket");

        var departure = Value(status, "departure_status");
        if (Is(departure, "Not Departed"))
            return Act("Did not fly: send back to Ticket");
        if (Is(departure, "Departed"))
            return Act("Send to Arrival");

        var flight = Date(status, "flight_date");
        if (!Is(Value(status, "notification_status"), "Notified"))
            return Act(flight is null ? "Notify candidate" : $"Notify candidate, flies {When(flight.Value, today)}");
        if (flight is not null && flight.Value > today)
            return Wait($"Notified, flies {When(flight.Value, today)}");
        return Act("Confirm departure");
    }

    public static Step Arrival(IReadOnlyDictionary<string, string> status)
    {
        var arrival = Value(status, "arrival");
        if (string.IsNullOrEmpty(arrival) || Is(arrival, "Pending"))
            return Act("Confirm arrival");
        if (Is(arrival, "Arrived") && !IsTrue(Value(status, "commission_linked")))
            return Act("Add to commission");
        return Wait(arrival);
    }

    /// <summary>
    /// Whether an arrival row is finished. An arrived candidate stays on the board after the
    /// commission is opened, and Returned and Runaway are handled as exception cases; none of
    /// those is anything left for the arrivals desk.
    /// </summary>
    public static bool ArrivalDone(IReadOnlyDictionary<string, string> status)
    {
        var arrival = Value(status, "arrival");
        if (Is(arrival, "Returned") || Is(arrival, "Runaway"))
            return true;
        return Is(arrival, "Arrived") && IsTrue(Value(status, "commission_linked"));
    }

    private static void Appointment(
        DateOnly? date, DateOnly today, string what, List<string> todo, List<string> waiting)
    {
        if (date is null)
            todo.Add($"Record {what} result");
        else if (date.Value > today)
            waiting.Add($"{Capitalise(what)} {When(date.Value, today)}");
        else
            todo.Add($"Record {what} result");
    }

    /// <summary>"today", "tomorrow", "in 5 days" — a date close enough to count in days.</summary>
    public static string When(DateOnly date, DateOnly today)
    {
        var days = date.DayNumber - today.DayNumber;
        return days switch
        {
            0 => "today",
            1 => "tomorrow",
            > 1 and <= 14 => $"in {days} days",
            _ => "on " + date.ToString("d MMM", CultureInfo.InvariantCulture),
        };
    }

    private static string Ago(DateOnly date, DateOnly today)
    {
        var days = today.DayNumber - date.DayNumber;
        return days switch
        {
            <= 0 => "today",
            1 => "yesterday",
            _ => $"{days} days ago",
        };
    }

    private static string Capitalise(string s) =>
        s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string? Value(IReadOnlyDictionary<string, string> status, string key) =>
        status.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

    private static DateOnly? Date(IReadOnlyDictionary<string, string> status, string key) =>
        Value(status, key) is { } raw &&
        DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
            ? d
            : null;

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);

    private static bool IsTrue(string? value) => Is(value, "true") || value == "1";
}
