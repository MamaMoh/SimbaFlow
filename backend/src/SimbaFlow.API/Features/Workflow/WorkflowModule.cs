using Carter;
using MediatR;
using SimbaFlow.API.Features.Workflow.Commands;
using SimbaFlow.API.Features.Workflow.Queries;

namespace SimbaFlow.API.Features.Workflow;

public class WorkflowModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/workflow")
            .WithTags("Workflow")
            .RequireAuthorization();

        // Get available actions for a candidate
        group.MapGet("/{candidateId:guid}/actions", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetAvailableActionsQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Execute a workflow transition
        group.MapPost("/{candidateId:guid}/transition", async (
            Guid candidateId, ExecuteTransitionRequest request, ISender sender) =>
        {
            var command = new ExecuteTransitionCommand(candidateId, request.TransitionRuleId, request.Notes);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Update a status field (within current stage)
        group.MapPost("/{candidateId:guid}/status", async (
            Guid candidateId, UpdateStatusRequest request, ISender sender) =>
        {
            var command = new UpdateStatusCommand(candidateId, request.TrackName, request.NewValue, request.Notes);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get workflow state for a candidate
        group.MapGet("/{candidateId:guid}/state", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetWorkflowStateQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get workflow event stream (timeline)
        group.MapGet("/{candidateId:guid}/events", async (Guid candidateId, ISender sender) =>
        {
            var result = await sender.Send(new GetWorkflowEventsQuery(candidateId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Get candidates in a specific stage view
        group.MapGet("/views/{stageId:guid}/candidates", async (
            Guid stageId, int? page, int? pageSize, string? search, ISender sender) =>
        {
            var query = new GetViewCandidatesQuery(stageId, page ?? 1, pageSize ?? 20, search);
            var result = await sender.Send(query);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // ──── Workflow Configuration (Admin) ────

        var configGroup = app.MapGroup("/api/workflow/config")
            .WithTags("Workflow Configuration")
            .RequireAuthorization();

        // Get full workflow definition for the tenant
        configGroup.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetWorkflowDefinitionQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Create stage
        configGroup.MapPost("/stages", async (CreateStageCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/workflow/config/stages/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Update stage
        configGroup.MapPut("/stages/{stageId:guid}", async (Guid stageId, UpdateStageRequest request, ISender sender) =>
        {
            var command = new UpdateStageCommand(stageId, request.Name, request.Description, request.SortOrder, request.StageType);
            var result = await sender.Send(command);
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Create transition rule
        configGroup.MapPost("/transitions", async (CreateTransitionRuleCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/workflow/config/transitions/{result.Data}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        // Delete (retire) a stage
        configGroup.MapDelete("/stages/{stageId:guid}", async (Guid stageId, ISender sender) =>
        {
            var result = await sender.Send(new DeleteStageCommand(stageId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Update a transition rule (label, required fields, WHO can perform it)
        configGroup.MapPut("/transitions/{transitionId:guid}", async (
            Guid transitionId, UpdateTransitionRequest request, ISender sender) =>
        {
            var result = await sender.Send(new UpdateTransitionRuleCommand(
                transitionId, request.ButtonLabel, request.ButtonIcon,
                request.RequiredFields, request.AllowedRoles,
                request.RemoveFromSource, request.IsActive));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Delete a transition rule
        configGroup.MapDelete("/transitions/{transitionId:guid}", async (Guid transitionId, ISender sender) =>
        {
            var result = await sender.Send(new DeleteTransitionRuleCommand(transitionId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Configure parallel tracks for a stage
        configGroup.MapPut("/stages/{stageId:guid}/tracks", async (
            Guid stageId, List<ParallelTrackInput> tracks, ISender sender) =>
        {
            var result = await sender.Send(new ConfigureParallelTracksCommand(stageId, tracks));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

// Request DTOs
public record ExecuteTransitionRequest(Guid TransitionRuleId, string? Notes);
public record UpdateStatusRequest(string TrackName, string NewValue, string? Notes);
public record UpdateStageRequest(string Name, string? Description, int SortOrder, int StageType);
public record UpdateTransitionRequest(
    string ButtonLabel, string? ButtonIcon, string[]? RequiredFields,
    string[]? AllowedRoles, bool RemoveFromSource, bool IsActive);
