using Carter;
using MediatR;
using SimbaFlow.API.Features.Exceptions.Commands;
using SimbaFlow.API.Features.Exceptions.Queries;

namespace SimbaFlow.API.Features.Exceptions;

public class ExceptionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/exceptions")
            .WithTags("Exceptions")
            .RequireAuthorization();

        group.MapGet("/", async (
            int? page, int? pageSize, string? status, string? type, ISender sender) =>
        {
            var result = await sender.Send(new GetExceptionCasesQuery(
                page ?? 1, pageSize ?? 20, status, type));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetExceptionCaseByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/{id:guid}/notes", async (Guid id, AddNoteRequest body, ISender sender) =>
        {
            var result = await sender.Send(new AddInvestigationNoteCommand(
                id, body.Body, body.AttachmentDocumentIds));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPatch("/{id:guid}/status", async (Guid id, UpdateStatusRequest body, ISender sender) =>
        {
            var result = await sender.Send(new UpdateExceptionStatusCommand(id, body.Status));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/{id:guid}/liabilities", async (Guid id, AssignLiabilityRequest body, ISender sender) =>
        {
            var result = await sender.Send(new AssignLiabilityCommand(
                id, body.Party, body.Amount, body.Currency ?? "ETB", body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/{id:guid}/close", async (Guid id, CloseExceptionRequest body, ISender sender) =>
        {
            var result = await sender.Send(new CloseExceptionCommand(
                id, body.ResolutionSummary, body.FinancialImpactAmount, body.FinancialImpactCurrency));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record AddNoteRequest(string Body, Guid[]? AttachmentDocumentIds);
public record UpdateStatusRequest(string Status);
public record AssignLiabilityRequest(string Party, decimal Amount, string? Currency, string? Notes);
public record CloseExceptionRequest(
    string ResolutionSummary,
    decimal? FinancialImpactAmount,
    string? FinancialImpactCurrency);
