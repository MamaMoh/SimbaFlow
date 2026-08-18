using Carter;
using MediatR;
using SimbaFlow.API.Features.Candidates.Commands;
using SimbaFlow.API.Features.Candidates.Queries;
using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

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
            Guid? stageId, string? countryOfTravel,
            ISender sender) =>
        {
            var query = new GetCandidatesQuery(
                page ?? 1, pageSize ?? 20, search, stageId, countryOfTravel);
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

        // Upload document (documentType from query or form field)
        // Listing was never mapped even though GetCandidateDocumentsHandler existed, so the
        // Documents tab answered 405 and stayed empty however many files were uploaded.
        group.MapGet("/{candidateId:guid}/documents", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetCandidateDocumentsQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/{candidateId:guid}/documents/{documentId:guid}", async (
            Guid candidateId,
            Guid documentId,
            ITenantDbContext context,
            IFileStorageService storage) =>
        {
            var doc = await context.CandidateDocuments.AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == documentId && d.CandidateId == candidateId && !d.IsDeleted);
            if (doc is null)
                return Results.Json(Result.Failure("Document not found", 404), statusCode: 404);

            var stream = await storage.DownloadAsync(doc.FilePath);
            if (stream is null)
                return Results.Json(Result.Failure("File is missing from storage", 404), statusCode: 404);

            return Results.File(stream, doc.ContentType, doc.OriginalFileName);
        });

        group.MapPost("/{candidateId:guid}/documents", async (
            Guid candidateId, HttpRequest httpRequest, ISender sender) =>
        {
            if (!httpRequest.HasFormContentType)
                return Results.Json(Result.Failure("Expected multipart form data", 400), statusCode: 400);

            var form = await httpRequest.ReadFormAsync();
            var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
            if (file is null)
                return Results.Json(Result.Failure("File is required", 400), statusCode: 400);

            var typeRaw = form["documentType"].FirstOrDefault()
                ?? httpRequest.Query["documentType"].FirstOrDefault();
            if (!int.TryParse(typeRaw, out var documentType))
                return Results.Json(Result.Failure("documentType is required", 400), statusCode: 400);

            var command = new UploadDocumentCommand(candidateId, file, documentType);
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/candidates/{candidateId}/documents/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        }).DisableAntiforgery();

        // Download candidate media (photo / full-photo / passport) for UI previews
        group.MapGet("/{candidateId:guid}/media/{kind}", async (Guid candidateId, string kind, ISender sender) =>
        {
            var result = await sender.Send(new GetCandidateMediaQuery(candidateId, kind));
            if (!result.IsSuccess || result.Data is null)
                return Results.Json(result, statusCode: result.StatusCode);
            return Results.File(result.Data.Bytes, result.Data.ContentType, result.Data.FileName);
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

        // Bulk generate CVs → ZIP
        group.MapPost("/cv/bulk", async (GenerateBulkCvCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.File(result.Data!, "application/zip", $"cvs_{DateTime.UtcNow:yyyyMMddHHmmss}.zip")
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Generate Enjaz / visa application form
        group.MapPost("/{candidateId:guid}/visa-form", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GenerateVisaFormCommand(candidateId));
            return result.IsSuccess
                ? Results.File(result.Data!, "application/pdf", $"visa_{candidateId}.pdf")
                : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
