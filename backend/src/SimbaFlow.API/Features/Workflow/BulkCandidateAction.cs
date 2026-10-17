using MediatR;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Workflow;

/// <summary>The candidates a bulk action was asked for, and anything the action needs from the form.</summary>
public record BulkCandidatesRequest(List<Guid> CandidateIds, string? Notes = null);

/// <summary>A candidate the action could not be applied to, and why — the same words the row menu would show.</summary>
public record BulkActionFailure(Guid CandidateId, string Error);

/// <summary>Who the action was applied to and who it was not.</summary>
public record BulkActionResult(List<Guid> Succeeded, List<BulkActionFailure> Failed);

/// <summary>
/// One board action, applied to many candidates.
///
/// The desk ticks twenty candidates and marks their insurance paid, rather than opening twenty row
/// menus. Each candidate still goes through the same command the row menu sends, one at a time —
/// with its permission check, its rules and its own audit entry — so a bulk action can never do
/// something to a candidate that the row menu would have refused. The browser checks the rules first
/// and only sends candidates that pass; this is the second check, for anything that changed in
/// between, and what it refuses is reported back by candidate rather than failing the batch.
/// </summary>
public static class BulkCandidateAction
{
    /// <summary>
    /// Enough for any batch a desk works in one sitting, and short of the request timing out while
    /// the person waits.
    /// </summary>
    public const int MaxCandidates = 100;

    public static async Task<Result<BulkActionResult>> RunAsync(
        IEnumerable<Guid>? candidateIds,
        Func<Guid, IRequest<Result>> command,
        ISender sender,
        ITenantDbContext context,
        CancellationToken ct = default)
    {
        var ids = (candidateIds ?? [])
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (ids.Count == 0)
            return Result<BulkActionResult>.Failure("Select at least one candidate.", 400);
        if (ids.Count > MaxCandidates)
            return Result<BulkActionResult>.Failure(
                $"Select at most {MaxCandidates} candidates at a time.", 400);

        var succeeded = new List<Guid>();
        var failed = new List<BulkActionFailure>();

        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();

            var result = await sender.Send(command(id), ct);

            // Not having the permission is the same answer for every candidate; repeating it a
            // hundred times would bury it, and trying the rest would only collect it ninety-nine
            // more times.
            if (result.StatusCode is 401 or 403)
                return Result<BulkActionResult>.Failure(result.Error ?? "Not allowed.", result.StatusCode);

            if (result.IsSuccess) succeeded.Add(id);
            else failed.Add(new BulkActionFailure(id, result.Error ?? "Not allowed."));

            // Every candidate starts from what is in the database. A command that changed an entity
            // and then refused would otherwise leave the change tracked, and the next candidate's
            // save would write it.
            if (context is DbContext db) db.ChangeTracker.Clear();
        }

        return Result<BulkActionResult>.Success(new BulkActionResult(succeeded, failed));
    }

    /// <summary>The usual answer: 200 with who succeeded and who did not, or the error.</summary>
    public static IResult ToHttp(Result<BulkActionResult> result) =>
        result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
}
