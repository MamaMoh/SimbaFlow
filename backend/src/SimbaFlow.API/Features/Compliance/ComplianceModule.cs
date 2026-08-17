using Carter;
using MediatR;
using SimbaFlow.API.Features.Compliance.Queries;

namespace SimbaFlow.API.Features.Compliance;

public class ComplianceModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/compliance")
            .WithTags("Compliance")
            .RequireAuthorization();

        group.MapGet("/alerts", async (ISender sender) =>
        {
            var result = await sender.Send(new GetComplianceAlertsQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
