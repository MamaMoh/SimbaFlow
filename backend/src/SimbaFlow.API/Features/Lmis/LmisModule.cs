using Carter;
using MediatR;
using SimbaFlow.API.Features.Lmis.Commands;
using SimbaFlow.API.Features.Lmis.Queries;
using SimbaFlow.API.Features.Workflow;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Lmis;

public class LmisModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lmis")
            .WithTags("LMIS")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page,
            int? pageSize,
            string? search,
            string? filters,
            string? insurance,
            string? milestone,
            bool? mirrorOnly,
            ISender sender) =>
        {
            var result = await sender.Send(new GetLmisBoardQuery(
                page ?? 1,
                pageSize ?? 20,
                search,
                filters,
                insurance,
                milestone,
                mirrorOnly));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Insurance for many candidates at once — the Actions menu on the board. Each candidate goes
        // through the row's own command; see BulkCandidateAction.
        group.MapPost("/bulk/insurance/paid", async (
            BulkCandidatesRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new RecordInsurancePaidCommand(id, Notes: body.Notes), sender, context, ct)));

        group.MapPost("/bulk/insurance/unpaid", async (
            BulkCandidatesRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new RecordInsuranceUnpaidCommand(id, body.Notes), sender, context, ct)));

        group.MapPost("/candidates/{id:guid}/insurance/paid", async (
            Guid id, InsurancePaidRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new RecordInsurancePaidCommand(
                id, body?.PaymentDate, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/insurance/unpaid", async (
            Guid id, NotesBody? body, ISender sender) =>
        {
            var result = await sender.Send(new RecordInsuranceUnpaidCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/portal-status", async (
            Guid id, PortalStatusBody body, ISender sender) =>
        {
            var result = await sender.Send(new RecordLmisStatusCommand(id, body.Status, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/milestone", async (
            Guid id, MilestoneRequest body, ISender sender) =>
        {
            var result = await sender.Send(new AdvanceLmisMilestoneCommand(
                id, body.Milestone, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record InsurancePaidRequest(DateOnly? PaymentDate, string? Notes);
public record MilestoneRequest(string Milestone, string? Notes);

public record NotesBody(string? Notes);
public record PortalStatusBody(string Status, string? Notes);
