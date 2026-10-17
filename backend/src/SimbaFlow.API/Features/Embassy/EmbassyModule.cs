using Carter;
using MediatR;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.API.Features.Embassy.Queries;
using SimbaFlow.API.Features.Workflow;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.API.Features.Embassy;

public class EmbassyModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embassy")
            .WithTags("Embassy")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page, int? pageSize, string? search, string? filters, ISender sender) =>
        {
            var result = await sender.Send(new GetEmbassyBoardQuery(
                page ?? 1, pageSize ?? 20, search, filters));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/case-executive/board", async (
            int? page, int? pageSize, string? search, string? filters, ISender sender) =>
        {
            var result = await sender.Send(new GetCaseExecutiveBoardQuery(
                page ?? 1, pageSize ?? 20, search, filters));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // The slip a candidate carries to their Tasheer appointment
        // The selected candidates as a Tasheer submission sheet
        group.MapPost("/tasheer/export", async (ExportTasheerListCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.File(
                    result.Data!,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"tasheer-list-{DateTime.UtcNow:yyyyMMdd}.xlsx")
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // The same five actions for many candidates at once — the Actions menu on the board. Each
        // candidate goes through the row's own command; see BulkCandidateAction.
        group.MapPost("/bulk/medical/book", async (
            BulkCandidatesRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new BookMedicalCommand(id, Notes: body.Notes), sender, context, ct)));

        group.MapPost("/bulk/medical/unbook", async (
            BulkCandidatesRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new UnbookMedicalCommand(id, body.Notes), sender, context, ct)));

        group.MapPost("/bulk/medical/result", async (
            BulkResultRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new RecordMedicalResultCommand(id, body.Result, body.Notes), sender, context, ct)));

        group.MapPost("/bulk/tasheer/book", async (
            BulkTasheerBookRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new BookTasheerCommand(id, body.AppointmentDate, body.Notes), sender, context, ct)));

        group.MapPost("/bulk/tasheer/result", async (
            BulkResultRequest body, ISender sender, ITenantDbContext context, CancellationToken ct) =>
            BulkCandidateAction.ToHttp(await BulkCandidateAction.RunAsync(
                body.CandidateIds, id => new RecordTasheerResultCommand(id, body.Result, body.Notes), sender, context, ct)));

        group.MapPost("/candidates/{id:guid}/medical/book", async (
            Guid id, BookMedicalRequest body, ISender sender) =>
        {
            var result = await sender.Send(new BookMedicalCommand(
                id, body.AppointmentDate, body.FacilityName, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/medical/unbook", async (
            Guid id, NotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new UnbookMedicalCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/medical/result", async (
            Guid id, MedicalResultRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordMedicalResultCommand(id, body.Result, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/tasheer/book", async (
            Guid id, BookTasheerRequest body, ISender sender) =>
        {
            var result = await sender.Send(new BookTasheerCommand(id, body.AppointmentDate, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/tasheer/result", async (
            Guid id, TasheerResultRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordTasheerResultCommand(id, body.Result, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/visa/ready", async (
            Guid id, NotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new SetVisaReadyCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/visa/submit", async (
            Guid id, SubmitVisaRequest body, ISender sender) =>
        {
            var result = await sender.Send(new SubmitVisaDocumentationCommand(
                id, body.SubmissionDate, body.ReferenceNumber, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/visa/outcome", async (
            Guid id, VisaOutcomeRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordVisaOutcomeCommand(
                id, body.Outcome, body.RejectionReason, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/visa/resubmit", async (
            Guid id, NotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new ResubmitVisaCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record BookMedicalRequest(DateOnly? AppointmentDate = null, string? FacilityName = null, string? Notes = null);
public record MedicalResultRequest(string Result, string? Notes);
public record BookTasheerRequest(DateOnly AppointmentDate, string? Notes);
public record TasheerResultRequest(string Result, string? Notes);
public record NotesRequest(string? Notes);
public record BulkResultRequest(List<Guid> CandidateIds, string Result, string? Notes = null);
public record BulkTasheerBookRequest(List<Guid> CandidateIds, DateOnly AppointmentDate, string? Notes = null);
public record SubmitVisaRequest(DateOnly? SubmissionDate, string? ReferenceNumber, string? Notes);
public record VisaOutcomeRequest(string Outcome, string? RejectionReason, string? Notes);
