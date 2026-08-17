using Carter;
using MediatR;
using SimbaFlow.API.Features.Arrival.Commands;
using SimbaFlow.API.Features.Arrival.Queries;

namespace SimbaFlow.API.Features.Arrival;

public class ArrivalModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/arrival")
            .WithTags("Arrival")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page, int? pageSize, string? search, Guid? officeId, ISender sender) =>
        {
            var result = await sender.Send(new GetArrivalBoardQuery(
                page ?? 1, pageSize ?? 20, search, officeId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/arrived", async (
            Guid id, ArrivalNotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new ConfirmArrivedCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/flag-exception", async (
            Guid id, FlagExceptionRequest body, ISender sender) =>
        {
            var result = await sender.Send(new FlagExceptionCommand(id, body.Type, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/add-to-commission", async (
            Guid id, ArrivalNotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new AddToCommissionCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record ArrivalNotesRequest(string? Notes);
public record FlagExceptionRequest(string Type, string? Notes);
