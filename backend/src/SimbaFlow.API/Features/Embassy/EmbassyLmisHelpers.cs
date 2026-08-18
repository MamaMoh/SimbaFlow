using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Entities.Workflow;
using SimbaFlow.Infrastructure.Persistence.Seeds;

namespace SimbaFlow.API.Features.Embassy;

internal static class EmbassyLmisHelpers
{
    public const string EmbassyStageName = "Embassy";
    public const string CaseExecutiveStageName = WorkflowDefinitionUpgrader.CaseExecutiveStageName;
    public const string LmisStageName = WorkflowDefinitionUpgrader.LmisStageName;

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
            status[prop.Name] = prop.Value.GetString() ?? "";

        return status;
    }

    public static string? TrackValue(Dictionary<string, string> status, string track) =>
        status.TryGetValue(track, out var v) ? v : null;

    public static int DaysInStage(Candidate candidate)
    {
        var from = candidate.StageEnteredAt ?? candidate.RegisteredAt;
        return Math.Max(0, (int)(DateTime.UtcNow.Date - from.Date).TotalDays);
    }

    /// <summary>
    /// Days since the candidate entered the pipeline — the whole-file age, not the age in the
    /// current stage. A candidate shuffled between stages looks fresh by DaysInStage while having
    /// been open for months, which is exactly the case supervisors need to see.
    /// </summary>
    public static int DaysSinceRegistered(Candidate candidate) =>
        Math.Max(0, (int)(DateTime.UtcNow.Date - candidate.RegisteredAt.Date).TotalDays);
}
