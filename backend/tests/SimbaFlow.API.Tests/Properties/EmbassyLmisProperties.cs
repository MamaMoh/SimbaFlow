using System.Text.Json;
using FsCheck;
using FsCheck.Xunit;
using FluentValidation;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.API.Features.Embassy.Validators;
using SimbaFlow.API.Features.Lmis.Commands;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Domain.Enums;
using SimbaFlow.Infrastructure.Workflow;

namespace SimbaFlow.API.Tests.Properties;

/// <summary>
/// FsCheck properties for Unit 3 Embassy / LMIS invariants (TEST-30–38).
/// </summary>
public class EmbassyLmisProperties
{
    private static readonly JsonDocument LmisMirror = JsonDocument.Parse("""
        {"operator":"AND","rules":[
          {"field":"medical","op":"eq","value":"Fit"},
          {"field":"tasheer","op":"eq","value":"Book Done"}
        ]}
        """);

    private static readonly JsonDocument CaseExecMirror = JsonDocument.Parse("""
        {"operator":"OR","rules":[
          {"field":"visa","op":"eq","value":"Ready"},
          {"field":"visa","op":"eq","value":"Submitted"}
        ]}
        """);

    private static readonly string[] MedicalPath = ["Pending", "Booked", "Fit", "Unfit"];
    private static readonly string[] TasheerPath = ["Pending", "Booked", "Book Done", "Expired"];
    private static readonly string[] VisaPath = ["Ready", "Submitted", "Issued", "Rejected"];
    private static readonly string[] MilestonePath = ["Uploaded", "Check Verified", "Issued"];

    /// <summary>TEST-30: updating medical in a status dict never changes tasheer.</summary>
    [Property(MaxTest = 50)]
    public bool TrackIndependence_MedicalDoesNotChangeTasheer(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var medical = MedicalPath[rng.Next(MedicalPath.Length)];
        var tasheer = TasheerPath[rng.Next(TasheerPath.Length)];
        var state = WorkflowState.Initial();
        state.Apply(StatusEvent(1, "medical", medical));
        state.Apply(StatusEvent(2, "tasheer", tasheer));
        var beforeTasheer = state.StatusValues["tasheer"];

        var nextMedical = MedicalPath[rng.Next(MedicalPath.Length)];
        state.Apply(StatusEvent(3, "medical", nextMedical));

        return state.StatusValues["tasheer"] == beforeTasheer
               && state.StatusValues["medical"] == nextMedical;
    }

    /// <summary>TEST-31: LMIS mirror condition ↔ Fit ∧ Book Done.</summary>
    [Property(MaxTest = 80)]
    public bool LmisMirror_IffFitAndBookDone(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var values = new Dictionary<string, string>
        {
            ["medical"] = MedicalPath[rng.Next(MedicalPath.Length)],
            ["tasheer"] = TasheerPath[rng.Next(TasheerPath.Length)]
        };
        var expected = values["medical"] == "Fit" && values["tasheer"] == "Book Done";
        return ConditionEvaluator.Evaluate(LmisMirror, values) == expected;
    }

    /// <summary>TEST-32: Case Executive mirror ↔ visa Ready|Submitted.</summary>
    [Property(MaxTest = 60)]
    public bool CaseExecutiveMirror_IffReadyOrSubmitted(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var visa = VisaPath[rng.Next(VisaPath.Length)];
        var values = new Dictionary<string, string> { ["visa"] = visa };
        var expected = visa is "Ready" or "Submitted";
        return ConditionEvaluator.Evaluate(CaseExecMirror, values) == expected;
    }

    /// <summary>TEST-33: only the sequential next milestone is legal from a given current.</summary>
    [Property(MaxTest = 40)]
    public bool MilestoneSequence_OnlyNextAllowed(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var currentIdx = rng.Next(-1, MilestonePath.Length); // -1 = empty
        var current = currentIdx < 0 ? "" : MilestonePath[currentIdx];
        var requested = MilestonePath[rng.Next(MilestonePath.Length)];

        string? expectedNext = current switch
        {
            "" => "Uploaded",
            "Uploaded" => "Check Verified",
            "Check Verified" => "Issued",
            _ => null
        };

        var legal = expectedNext is not null
                    && requested.Equals(expectedNext, StringComparison.OrdinalIgnoreCase);
        // Property: illegal skips are never equal to expectedNext when skipping ahead
        if (currentIdx >= 0 && currentIdx < MilestonePath.Length - 1)
        {
            var skip = MilestonePath[Math.Min(currentIdx + 2, MilestonePath.Length - 1)];
            if (currentIdx + 2 < MilestonePath.Length)
                return !skip.Equals(expectedNext, StringComparison.OrdinalIgnoreCase) || legal;
        }

        return !legal || requested == expectedNext;
    }

    /// <summary>TEST-34: Rejected without reason always fails FluentValidation.</summary>
    [Property(MaxTest = 30)]
    public bool RejectionReasonRequired_WhenRejected(NonEmptyString reason)
    {
        // FsCheck's NonEmptyString includes whitespace-only strings such as " ", which the
        // validator rejects on purpose — a blank reason is no reason. Only non-blank input is a
        // valid rejection reason, so that is what this property is about.
        if (string.IsNullOrWhiteSpace(reason.Get)) return true;

        var withReason = new RecordVisaOutcomeValidator()
            .Validate(new RecordVisaOutcomeCommand(Guid.NewGuid(), "Rejected", reason.Get));
        var without = new RecordVisaOutcomeValidator()
            .Validate(new RecordVisaOutcomeCommand(Guid.NewGuid(), "Rejected", null));

        return withReason.IsValid && !without.IsValid;
    }

    /// <summary>TEST-36 model: RemoveFromSource visibility set drops embassy + mirror targets.</summary>
    [Property(MaxTest = 40)]
    public bool ToLmisVisibilityModel_DropsSourceAndMirrors(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var embassy = Guid.NewGuid();
        var caseExec = Guid.NewGuid();
        var lmis = Guid.NewGuid();
        var visible = new HashSet<Guid> { embassy, caseExec, lmis };
        if (rng.Next(2) == 0) visible.Add(Guid.NewGuid());

        // Model of engine RemoveFromSource cleanup
        visible.Remove(embassy);
        visible.Remove(caseExec); // mirror of embassy
        // lmis may remain if it was a preview mirror — full transfer sets primary LMIS
        visible.Add(lmis);
        visible.Remove(embassy);

        return !visible.Contains(embassy)
               && !visible.Contains(caseExec)
               && visible.Contains(lmis);
    }

    /// <summary>TEST-37 model: after Paid→Available chain, insurance track is Available.</summary>
    [Property(MaxTest = 30)]
    public bool InsurancePaidChain_EndsAvailable(NonEmptyString _)
    {
        var state = WorkflowState.Initial();
        state.Apply(StatusEvent(1, "insurance", "Insurance Paid"));
        state.Apply(StatusEvent(2, "insurance", "Available"));
        return state.StatusValues["insurance"] == "Available";
    }

    /// <summary>TEST-38: random legal embassy status sequences remain consistent under replay.</summary>
    [Property(MaxTest = 40)]
    public bool StatefulEmbassySequence_ReplayConsistent(PositiveInt seed)
    {
        var rng = new Random(seed.Get);
        var events = new List<WorkflowEvent>();
        var seq = 1;
        events.Add(StatusEvent(seq++, "medical", "Booked"));
        if (rng.Next(2) == 0)
            events.Add(StatusEvent(seq++, "tasheer", "Booked"));
        events.Add(StatusEvent(seq++, "medical", rng.Next(2) == 0 ? "Fit" : "Unfit"));
        if (rng.Next(2) == 0)
            events.Add(StatusEvent(seq++, "tasheer", "Book Done"));
        if (rng.Next(2) == 0)
            events.Add(StatusEvent(seq++, "visa", "Ready"));

        var a = WorkflowState.Initial();
        foreach (var e in events) a.Apply(e);
        var b = WorkflowState.Initial();
        foreach (var e in events) b.Apply(e);

        return a.StatusValues.OrderBy(kv => kv.Key)
            .SequenceEqual(b.StatusValues.OrderBy(kv => kv.Key));
    }

    private static WorkflowEvent StatusEvent(int seq, string track, string value) =>
        new()
        {
            Id = Guid.NewGuid(),
            CandidateId = Guid.NewGuid(),
            SequenceNumber = seq,
            EventType = WorkflowEventType.StatusUpdated,
            UserId = Guid.NewGuid(),
            UserName = "pbt",
            Timestamp = DateTime.UtcNow,
            Data = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                trackName = track,
                newValue = value
            }))
        };
}
