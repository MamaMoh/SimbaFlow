using Carter;
using MediatR;
using SimbaFlow.API.Features.Embassy.Commands;
using SimbaFlow.API.Features.Embassy.Queries;

namespace SimbaFlow.API.Features.Embassy;

public class EmbassyModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/embassy")
            .WithTags("Embassy")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page, int? pageSize, string? search, ISender sender) =>
        {
            var result = await sender.Send(new GetEmbassyBoardQuery(
                page ?? 1, pageSize ?? 20, search));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/case-executive/board", async (
            int? page, int? pageSize, string? search, ISender sender) =>
        {
            var result = await sender.Send(new GetCaseExecutiveBoardQuery(
                page ?? 1, pageSize ?? 20, search));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/medical/book", async (
            Guid id, BookMedicalRequest body, ISender sender) =>
        {
            var result = await sender.Send(new BookMedicalCommand(
                id, body.AppointmentDate, body.FacilityName, body.Notes));
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

public record BookMedicalRequest(DateOnly AppointmentDate, string FacilityName, string? Notes);
public record MedicalResultRequest(string Result, string? Notes);
public record BookTasheerRequest(DateOnly AppointmentDate, string? Notes);
public record TasheerResultRequest(string Result, string? Notes);
public record NotesRequest(string? Notes);
public record SubmitVisaRequest(DateOnly? SubmissionDate, string? ReferenceNumber, string? Notes);
public record VisaOutcomeRequest(string Outcome, string? RejectionReason, string? Notes);
