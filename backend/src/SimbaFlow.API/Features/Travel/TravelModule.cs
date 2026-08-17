using Carter;
using MediatR;
using SimbaFlow.API.Features.Travel.Commands;
using SimbaFlow.API.Features.Travel.Queries;

namespace SimbaFlow.API.Features.Travel;

public class TravelModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/travel")
            .WithTags("Travel")
            .RequireAuthorization();

        group.MapGet("/ticket/board", async (
            int? page, int? pageSize, string? search, Guid? officeId, ISender sender) =>
        {
            var result = await sender.Send(new GetTicketBoardQuery(
                page ?? 1, pageSize ?? 20, search, officeId));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/departure/board", async (
            int? page, int? pageSize, string? search, Guid? officeId, bool? includeCanceled, ISender sender) =>
        {
            var result = await sender.Send(new GetDepartureBoardQuery(
                page ?? 1, pageSize ?? 20, search, officeId, includeCanceled ?? false));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/ticket/book", async (
            Guid id, BookTicketRequest body, ISender sender) =>
        {
            var result = await sender.Send(new BookTicketCommand(
                id, body.Destination, body.FlightDate, body.TicketRef, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/notify", async (
            Guid id, TravelNotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new MarkNotifiedCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/departed", async (
            Guid id, TravelNotesRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new ConfirmDepartedCommand(id, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/not-departed", async (
            Guid id, NotDepartedRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordNotDepartedCommand(
                id, body.Reason, body.Outcome, body.ReasonOther, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record BookTicketRequest(string Destination, DateOnly FlightDate, string? TicketRef, string? Notes);
public record TravelNotesRequest(string? Notes);
public record NotDepartedRequest(string Reason, string Outcome, string? ReasonOther, string? Notes);
