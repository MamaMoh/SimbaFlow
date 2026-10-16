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
    /// <summary>
    /// The parallel tracks a stage runs, named as the desk names them.
    ///
    /// A stage is rarely one queue. Embassy runs medical, Tasheer and the visa side by side, and
    /// on a busy board the person booking medicals does not want the Tasheer rows in front of
    /// them. The track is what a step is about, so the board can be shown one track at a time.
    /// </summary>
    public static class Tracks
    {
        public const string Review = "Review";
        public const string Contract = "Contract";
        public const string Medical = "Medical";
        public const string Tasheer = "Tasheer";
        public const string Visa = "Visa";
        public const string Insurance = "Insurance";
        public const string Milestone = "Milestone";
        public const string Ticket = "Ticket";
        public const string Flight = "Flight";
        public const string Notification = "Notification";
        public const string Arrival = "Arrival";
        public const string Commission = "Commission";
        public const string Case = "Case";
        public const string Dispute = "Dispute";
        public const string Fees = "Fees";
        public const string Payment = "Payment";
    }

    public record Step(string Text, bool NeedsAction)
    {
        /// <summary>
        /// Which of the stage's tracks this step belongs to — usually one, and more when a
        /// candidate owes the same desk two things at once.
        /// </summary>
        public IReadOnlyList<string> Tracks { get; init; } = [];
    }

    private static Step Act(string text, params string[] tracks) => new(text, true) { Tracks = tracks };
    private static Step Wait(string text, params string[] tracks) => new(text, false) { Tracks = tracks };

    public static Step Intake() => Act("Review and send to New Contracts", Tracks.Review);

    public static Step NewContracts(IReadOnlyDictionary<string, string> status) =>
        Is(Value(status, "status"), "Ready")
            ? Act("Send to Embassy", Tracks.Contract)
            : Act("Review contract and mark Ready", Tracks.Contract);

    /// <summary>
    /// Embassy runs medical and Tasheer side by side and then the visa, so a candidate can owe
    /// the desk two things at once. Everything the desk owes is listed, joined; only when it
    /// owes nothing does the row read as waiting.
    /// </summary>
    public static Step Embassy(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        var todo = new List<Part>();
        var waiting = new List<Part>();

        var medical = Value(status, "medical");
        if (string.IsNullOrEmpty(medical) || Is(medical, "Pending"))
            todo.Add(new("Book medical", Tracks.Medical));
        else if (Is(medical, "Unfit"))
            todo.Add(new("Medical unfit: rebook or remove", Tracks.Medical));
        else if (Is(medical, "Booked"))
            Appointment(Date(status, "medical_appointment_date"), today, "medical", Tracks.Medical, todo, waiting);

        var tasheer = Value(status, "tasheer");
        if (string.IsNullOrEmpty(tasheer) || Is(tasheer, "Pending"))
            todo.Add(new("Book Tasheer", Tracks.Tasheer));
        else if (Is(tasheer, "Expired"))
            todo.Add(new("Tasheer expired: rebook", Tracks.Tasheer));
        else if (Is(tasheer, "Booked"))
            Appointment(Date(status, "tasheer_appointment_date"), today, "Tasheer", Tracks.Tasheer, todo, waiting);

        var visa = Value(status, "visa");
        var cleared = Is(medical, "Fit") && Is(tasheer, "Book Done");
        if (cleared && string.IsNullOrEmpty(visa))
            todo.Add(new("Mark visa Ready", Tracks.Visa));
        else if (Is(visa, "Ready"))
            waiting.Add(new("Visa with Case Executive", Tracks.Visa));
        else if (Is(visa, "Submitted"))
            waiting.Add(new("Visa submitted, waiting for result", Tracks.Visa));
        else if (Is(visa, "Rejected"))
            todo.Add(new("Visa rejected: resubmit", Tracks.Visa));
        else if (Is(visa, "Issued"))
            todo.Add(new("Send to LMIS", Tracks.Visa));

        // The tracks are the ones the row is about now: what the desk owes when it owes
        // something, and what it is waiting on when it does not. Filtering by Medical should
        // show the medicals to book, not everyone who once had a medical.
        if (todo.Count > 0)
            return Join(todo, true);
        return waiting.Count > 0 ? Join(waiting, false) : Wait("Waiting for clearances");
    }

    /// <summary>One thing a stage is waiting on, and which of its tracks that is.</summary>
    private readonly record struct Part(string Text, string Track);

    private static Step Join(List<Part> parts, bool needsAction) =>
        new(string.Join(" · ", parts.Select(p => p.Text)), needsAction)
        {
            Tracks = parts.Select(p => p.Track).Distinct().ToList(),
        };

    public static Step CaseExecutive(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        var visa = Value(status, "visa");
        if (Is(visa, "Submitted"))
        {
            var since = Date(status, "visa_submission_date");
            return Wait(since is null
                ? "Submitted, waiting for the embassy"
                : $"Submitted {Ago(since.Value, today)}, waiting for the embassy", Tracks.Visa);
        }
        return Act("Submit visa documents", Tracks.Visa);
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
            return Act(
                Is(insurance, "Insurance Paid") ? "Confirm insurance is available" : "Pay insurance",
                Tracks.Insurance);

        var milestone = Value(status, "milestone");
        if (string.IsNullOrEmpty(milestone))
            return Act("Upload to LMIS", Tracks.Milestone);
        if (Is(milestone, "Uploaded"))
            return Wait("Uploaded, waiting for the LMIS check", Tracks.Milestone);
        if (Is(milestone, "Check Verified"))
            return Wait("Verified, waiting for LMIS to issue", Tracks.Milestone);
        return Act("Send to Ticket", Tracks.Milestone);
    }

    public static Step Ticket(IReadOnlyDictionary<string, string> status)
    {
        if (IsTrue(Value(status, "canceled")))
            return Act("Ticket canceled: rebook", Tracks.Ticket);
        if (!Is(Value(status, "ticket_status"), "Booking Complete"))
            return Act("Book ticket", Tracks.Ticket);
        if (string.IsNullOrEmpty(Value(status, "destination")) || Date(status, "flight_date") is null)
            return Act("Add destination and flight date", Tracks.Flight);
        return Act("Send to Departure", Tracks.Ticket);
    }

    public static Step Departure(IReadOnlyDictionary<string, string> status, DateOnly today)
    {
        if (IsTrue(Value(status, "canceled")))
            return Act("Ticket canceled: send back to Ticket", Tracks.Ticket);

        var departure = Value(status, "departure_status");
        if (Is(departure, "Not Departed"))
            return Act("Did not fly: send back to Ticket", Tracks.Flight);
        if (Is(departure, "Departed"))
            return Act("Send to Arrival", Tracks.Flight);

        var flight = Date(status, "flight_date");
        if (!Is(Value(status, "notification_status"), "Notified"))
            return Act(
                flight is null ? "Notify candidate" : $"Notify candidate, flies {When(flight.Value, today)}",
                Tracks.Notification);
        if (flight is not null && flight.Value > today)
            return Wait($"Notified, flies {When(flight.Value, today)}", Tracks.Flight);
        return Act("Confirm departure", Tracks.Flight);
    }

    public static Step Arrival(IReadOnlyDictionary<string, string> status)
    {
        var arrival = Value(status, "arrival");
        if (string.IsNullOrEmpty(arrival) || Is(arrival, "Pending"))
            return Act("Confirm arrival", Tracks.Arrival);
        if (Is(arrival, "Arrived") && !IsTrue(Value(status, "commission_linked")))
            return Act("Add to commission", Tracks.Commission);
        return Wait(arrival, Tracks.Arrival);
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
        DateOnly? date, DateOnly today, string what, string track, List<Part> todo, List<Part> waiting)
    {
        if (date is null)
            todo.Add(new($"Record {what} result", track));
        else if (date.Value > today)
            waiting.Add(new($"{Capitalise(what)} {When(date.Value, today)}", track));
        else
            todo.Add(new($"Record {what} result", track));
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
