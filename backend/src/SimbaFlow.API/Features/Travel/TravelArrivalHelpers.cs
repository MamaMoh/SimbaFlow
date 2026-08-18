using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Features.Travel;

internal static class TravelArrivalHelpers
{
    public const string TicketStageName = WorkflowDefinitionUpgrader.TicketStageName;
    public const string DepartureStageName = WorkflowDefinitionUpgrader.DepartureStageName;
    public const string ArrivalStageName = WorkflowDefinitionUpgrader.ArrivalStageName;
    public const string CommissionStageName = WorkflowDefinitionUpgrader.CommissionStageName;

    public const string ToDepartureAction = WorkflowDefinitionUpgrader.ToDepartureAction;
    public const string ToArrivalAction = WorkflowDefinitionUpgrader.ToArrivalAction;
    public const string BackToTicketAction = WorkflowDefinitionUpgrader.BackToTicketAction;
    public const string AddToCommissionAction = WorkflowDefinitionUpgrader.AddToCommissionAction;

    public const string ArrivalTrack = "arrival";

    public static async Task<WorkflowStage?> FindStageByNameAsync(
        ITenantDbContext context, string name, CancellationToken ct)
    {
        // EF Core cannot translate string.Equals(..., StringComparison); use ToLower().
        var needle = name.ToLower();
        return await context.WorkflowStages
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                !s.IsDeleted && s.Name.ToLower() == needle, ct);
    }

    public static bool IsVisibleInStage(Candidate candidate, Guid stageId) =>
        candidate.CurrentStageId == stageId || candidate.VisibleInStages.Contains(stageId);

    public static Dictionary<string, string> ReadStatusValues(Candidate candidate)
    {
        var status = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (candidate.CurrentStatusValues is null ||
            candidate.CurrentStatusValues.RootElement.ValueKind != JsonValueKind.Object)
            return status;

        foreach (var prop in candidate.CurrentStatusValues.RootElement.EnumerateObject())
            status[prop.Name] = prop.Value.GetString() ?? prop.Value.ToString();

        return status;
    }

    public static string? TrackValue(Dictionary<string, string> status, string track) =>
        status.TryGetValue(track, out var v) ? v : null;

    public static bool IsCanceled(Dictionary<string, string> status)
    {
        var v = TrackValue(status, "canceled");
        return string.Equals(v, "true", StringComparison.OrdinalIgnoreCase)
               || string.Equals(v, "1", StringComparison.OrdinalIgnoreCase);
    }

    public static int DaysInStage(Candidate candidate)
    {
        var from = candidate.StageEnteredAt ?? candidate.RegisteredAt;
        return Math.Max(0, (int)(DateTime.UtcNow.Date - from.Date).TotalDays);
    }

    /// <summary>Days since the candidate entered the pipeline (whole-file age).</summary>
    public static int DaysSinceRegistered(Candidate candidate) =>
        Math.Max(0, (int)(DateTime.UtcNow.Date - candidate.RegisteredAt.Date).TotalDays);

    public static int? RemainingDays(Dictionary<string, string> status)
    {
        var raw = TrackValue(status, "flight_date");
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var flight))
            return null;
        return flight.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
    }

    public static async Task<WorkflowTransitionRule?> FindTransitionAsync(
        ITenantDbContext context,
        Guid sourceStageId,
        Guid targetStageId,
        string buttonLabel,
        CancellationToken ct) =>
        await context.WorkflowTransitionRules
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                !r.IsDeleted
                && r.IsActive
                && r.SourceStageId == sourceStageId
                && r.TargetStageId == targetStageId
                && r.ButtonLabel == buttonLabel, ct);

    public static string NormalizeReason(string reason) => reason.Trim() switch
    {
        var r when r.Equals("MissedFlight", StringComparison.OrdinalIgnoreCase) => "MissedFlight",
        var r when r.Equals("Immigration", StringComparison.OrdinalIgnoreCase) => "Immigration",
        var r when r.Equals("Medical", StringComparison.OrdinalIgnoreCase) => "Medical",
        var r when r.Equals("CandidateNoShow", StringComparison.OrdinalIgnoreCase) => "CandidateNoShow",
        var r when r.Equals("AirlineCancel", StringComparison.OrdinalIgnoreCase) => "AirlineCancel",
        var r when r.Equals("Other", StringComparison.OrdinalIgnoreCase) => "Other",
        _ => reason.Trim()
    };
}
