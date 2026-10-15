using Carter;
using MediatR;
using SimbaFlow.API.Features.Reports.Schedules;

namespace SimbaFlow.API.Features.Reports;

public class ReportScheduleModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/report-schedules")
            .WithTags("Reports")
            .RequireAuthorization();

        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetReportSchedulesQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/", async (CreateReportScheduleCommand command, ISender sender) =>
        {
            var result = await sender.Send(command);
            return result.IsSuccess
                ? Results.Created($"/api/report-schedules/{result.Data!.Id}", result)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateReportScheduleCommand command, ISender sender) =>
        {
            var result = await sender.Send(command with { Id = id });
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new DeleteReportScheduleCommand(id));
            return result.IsSuccess ? Results.NoContent() : Results.Json(result, statusCode: result.StatusCode);
        });

        // Send the report now, to check what arrives. The regular schedule is unchanged.
        group.MapPost("/{id:guid}/send", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new SendReportScheduleNowCommand(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
