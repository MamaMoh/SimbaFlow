using Carter;
using MediatR;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.API.Features.Candidates.Queries;

namespace SimbaFlow.API.Features.Candidates;

public class CandidateModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/candidates")
            .WithTags("Candidates")
            .RequireAuthorization();

        // List candidates (paginated, searchable, filterable)
        group.MapGet("/", async (
            int? page, int? pageSize, string? search,
            Guid? stageId, Guid? officeId, string? countryOfTravel,
            ISender sender) =>
        {
            var query = new GetCandidatesQuery(
                page ?? 1, pageSize ?? 20, search, stageId, officeId, countryOfTravel);
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get candidate detail
        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCandidateByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Register new candidate
        group.MapPost("/", async (RegisterCandidateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/candidates/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Update candidate
        group.MapPut("/{id:guid}", async (Guid id, UpdateCandidateCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { Id = id });
            var result2 = result;
            return result2.IsSuccess ? Results.Ok(result2) : Results.Json(result2, statusCode: result2.StatusCode);
        });

        // Soft delete candidate
        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteCandidateCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.Json(result, statusCode: result.StatusCode);
        });

        // Upload document
        group.MapPost("/{candidateId:guid}/documents", async (
            Guid candidateId, IFormFile file, int documentType, ISender sender) =>
        {
            var command = new UploadDocumentCommand(candidateId, file, documentType);
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/candidates/{candidateId}/documents/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).DisableAntiforgery();

        // Get candidate documents
        group.MapGet("/{candidateId:guid}/documents", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetCandidateDocumentsQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get candidate timeline (workflow events)
        group.MapGet("/{candidateId:guid}/timeline", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetCandidateTimelineQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Generate CV
        group.MapPost("/{candidateId:guid}/cv", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GenerateCVCommand(candidateId));
            return result.IsSuccess
                ? Results.File(result.Data!, "application/pdf", $"cv_{candidateId}.pdf")
                : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
