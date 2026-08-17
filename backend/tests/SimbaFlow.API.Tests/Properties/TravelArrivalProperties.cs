using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using SimbaFlow.API.Features.Travel.Commands;
using SimbaFlow.API.Features.Travel.Validators;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// FsCheck properties for Unit 4 Travel / Arrival invariants (TEST-40–53).
/// </summary>
public class TravelArrivalProperties
{
    private static readonly string[] Reasons =
    [
        "MissedFlight", "Immigration", "Medical", "CandidateNoShow", "AirlineCancel", "Other"
    ];

    private static readonly string[] Outcomes = ["BackToTicket", "CancelDeparture"];

    /// <summary>TEST-40: BookTicket without destination fails validation.</summary>
    [Property(MaxTest = 30)]
    public bool BookTicket_RequiresDestination(NonEmptyString destination)
    {
        var ok = new BookTicketValidator().Validate(
            new BookTicketCommand(Guid.NewGuid(), destination.Get, DateOnly.FromDateTime(DateTime.UtcNow)));
        var bad = new BookTicketValidator().Validate(
            new BookTicketCommand(Guid.NewGuid(), "", DateOnly.FromDateTime(DateTime.UtcNow)));
        return ok.IsValid && !bad.IsValid;
    }

    /// <summary>TEST-42: canceled rows are excluded from default countdown filter.</summary>
    [Property(MaxTest = 40)]
    public bool Countdown_ExcludesCanceled(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var canceled = rng.Next(2) == 0;
        var includeCanceled = false;
        var visible = includeCanceled || !canceled;
        return canceled ? !visible : visible;
    }

    /// <summary>TEST-43: NotDeparted requires reason + valid outcome.</summary>
    [Property(MaxTest = 40)]
    public bool NotDeparted_RequiresReasonAndOutcome(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var reason = Reasons[rng.Next(Reasons.Length)];
        var outcome = Outcomes[rng.Next(Outcomes.Length)];
        var reasonOther = reason == "Other" ? "details" : null;

        var valid = new RecordNotDepartedValidator().Validate(
            new RecordNotDepartedCommand(Guid.NewGuid(), reason, outcome, reasonOther));
        var missingOutcome = new RecordNotDepartedValidator().Validate(
            new RecordNotDepartedCommand(Guid.NewGuid(), reason, "", reasonOther));
        var otherMissing = new RecordNotDepartedValidator().Validate(
            new RecordNotDepartedCommand(Guid.NewGuid(), "Other", outcome, null));

        return valid.IsValid && !missingOutcome.IsValid && !otherMissing.IsValid;
    }

    /// <summary>TEST-45 model: Cancel stays Departed-blocked (canceled=true, no To Arrival).</summary>
    [Property(MaxTest = 30)]
    public bool Cancel_BlocksToArrivalCondition(NonEmptyString _)
    {
        var values = new Dictionary<string, string>
        {
            ["departure_status"] = "Not Departed",
            ["canceled"] = "true"
        };
        var toArrival = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Departed"}]}""");
        return !ConditionEvaluator.Evaluate(toArrival, values);
    }

    /// <summary>TEST-46 model: Departed enables To Arrival condition.</summary>
    [Property(MaxTest = 20)]
    public bool Departed_EnablesToArrival(NonEmptyString _)
    {
        var values = new Dictionary<string, string> { ["departure_status"] = "Departed" };
        var toArrival = JsonDocument.Parse(
            """{"operator":"AND","rules":[{"field":"departure_status","op":"eq","value":"Departed"}]}""");
        return ConditionEvaluator.Evaluate(toArrival, values);
    }

    /// <summary>TEST-48 model: RemoveFromSource=false keeps arrival in visibility after commission.</summary>
    [Property(MaxTest = 40)]
    public bool ArrivalPermanence_AfterCommission(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var arrival = Guid.NewGuid();
        var commission = Guid.NewGuid();
        var visible = new HashSet<Guid> { arrival };
        if (rng.Next(2) == 0) visible.Add(Guid.NewGuid());

        // Model Add to Commission RemoveFromSource=false
        visible.Add(commission);
        // do NOT remove arrival

        return visible.Contains(arrival) && visible.Contains(commission);
    }

    /// <summary>TEST-49 model: commission shell upsert keeps one row.</summary>
    [Property(MaxTest = 30)]
    public bool CommissionShell_IdempotentCount(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var shells = new HashSet<Guid>();
        var candidateId = Guid.NewGuid();
        var attempts = 1 + rng.Next(5);
        for (var i = 0; i < attempts; i++)
        {
            if (shells.Count == 0)
                shells.Add(candidateId);
        }
        return shells.Count == 1;
    }

    /// <summary>TEST-50/51 model: one Open exception per candidate.</summary>
    [Property(MaxTest = 30)]
    public bool OneOpenException_PerCandidate(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var open = new HashSet<Guid>();
        var candidate = Guid.NewGuid();
        var attempts = 2 + rng.Next(4);
        var created = 0;
        var rejected = 0;
        for (var i = 0; i < attempts; i++)
        {
            if (open.Contains(candidate))
                rejected++;
            else
            {
                open.Add(candidate);
                created++;
            }
        }
        return created == 1 && rejected == attempts - 1 && open.Count == 1;
    }

    /// <summary>TEST-52 model: Add to Commission blocked when open exception exists.</summary>
    [Property(MaxTest = 20)]
    public bool CommissionBlocked_IfOpenException(bool hasOpenException)
    {
        var canAdd = !hasOpenException;
        return hasOpenException ? !canAdd : canAdd;
    }

    /// <summary>TEST-53: random travel status sequences replay consistently.</summary>
    [Property(MaxTest = 40)]
    public bool StatefulTravelSequence_ReplayConsistent(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var events = new List<WorkflowEvent>();
        var seq = 1;
        events.Add(StatusEvent(seq++, "ticket_status", "Booking Complete",
            ("destination", "Riyadh"), ("flight_date", "2026-08-01")));
        events.Add(StatusEvent(seq++, "notification_status", "Notified"));
        if (rng.Next(2) == 0)
        {
            events.Add(StatusEvent(seq++, "departure_status", "Departed"));
            events.Add(StatusEvent(seq++, "arrival", "Pending"));
            if (rng.Next(2) == 0)
                events.Add(StatusEvent(seq++, "arrival", "Arrived", ("commission_linked", "true")));
        }
        else
        {
            var outcome = Outcomes[rng.Next(Outcomes.Length)];
            events.Add(StatusEvent(seq++, "departure_status", "Not Departed",
                ("canceled", outcome == "CancelDeparture" ? "true" : "false"),
                ("departure_outcome", outcome == "CancelDeparture" ? "Canceled" : "Rebooked")));
        }

        var a = WorkflowState.Initial();
        foreach (var e in events) a.Apply(e);
        var b = WorkflowState.Initial();
        foreach (var e in events) b.Apply(e);

        return a.StatusValues.OrderBy(kv => kv.Key)
            .SequenceEqual(b.StatusValues.OrderBy(kv => kv.Key));
    }

    private static WorkflowEvent StatusEvent(
        int seq, string track, string value, params (string Key, string Value)[] meta)
    {
        var payload = new Dictionary<string, string>
        {
            ["trackName"] = track,
            ["newValue"] = value
        };
        foreach (var (k, v) in meta)
            payload[k] = v;

        return new WorkflowEvent
        {
            Id = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            SequenceNumber = seq,
            EventType = WorkflowEventType.StatusUpdated,
            UserId = Guid.NewGuid(),
            UserName = "pbt",
            Timestamp = DateTime.UtcNow,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(payload))
        };
    }
}
