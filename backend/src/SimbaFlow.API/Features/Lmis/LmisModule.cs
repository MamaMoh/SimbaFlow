using Carter;
using MediatR;
using SimbaFlow.API.Features.Lmis.Commands;
using SimbaFlow.API.Features.Lmis.Queries;

namespace SimbaFlow.API.Features.Lmis;

public class LmisModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/lmis")
            .WithTags("LMIS")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page,
            int? pageSize,
            string? search,
            Guid? officeId,
            string? insurance,
            string? milestone,
            bool? mirrorOnly,
            ISender sender) =>
        {
            var result = await sender.Send(new GetLmisBoardQuery(
                page ?? 1,
                pageSize ?? 20,
                search,
                officeId,
                insurance,
                milestone,
                mirrorOnly));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/insurance/paid", async (
            Guid id, InsurancePaidRequest? body, ISender sender) =>
        {
            var result = await sender.Send(new RecordInsurancePaidCommand(
                id, body?.PaymentDate, body?.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/candidates/{id:guid}/milestone", async (
            Guid id, MilestoneRequest body, ISender sender) =>
        {
            var result = await sender.Send(new AdvanceLmisMilestoneCommand(
                id, body.Milestone, body.Notes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record InsurancePaidRequest(DateOnly? PaymentDate, string? Notes);
public record MilestoneRequest(string Milestone, string? Notes);
