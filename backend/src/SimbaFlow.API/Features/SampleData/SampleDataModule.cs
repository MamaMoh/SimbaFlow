using Carter;
using MediatR;

namespace SimbaFlow.API.Features.SampleData;

/// <summary>
/// Fill the pipeline with test candidates, or clear them out again.
/// </summary>
public class SampleDataModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/sample-data").RequireAuthorization();

        group.MapPost("/", async (ISender sender) =>
        {
            var result = await sender.Send(new SeedSampleDataCommand());
            return Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapDelete("/", async (ISender sender) =>
        {
            var result = await sender.Send(new RemoveSampleDataCommand());
            return Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
