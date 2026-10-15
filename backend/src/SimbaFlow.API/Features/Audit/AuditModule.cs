using Carter;
using MediatR;
using SimbaFlow.API.Features.Audit.Queries;

namespace SimbaFlow.API.Features.Audit;

public class AuditModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/audit")
            .WithTags("Audit")
            .RequireAuthorization();

        group.MapGet("/", async (
            ISender sender,
            string? kind = null,
            DateOnly? from = null,
            DateOnly? to = null,
            string? userId = null,
            string? action = null,
            string? entityId = null,
            bool failedOnly = false,
            int page = 1,
            int pageSize = 50) =>
        {
            var parsedKind = Enum.TryParse<AuditTrailKind>(kind, ignoreCase: true, out var k)
                ? k
                : AuditTrailKind.Changes;

            var result = await sender.Send(new GetAuditTrailQuery(
                parsedKind, from, to, userId, action, entityId, failedOnly, page, pageSize));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}
