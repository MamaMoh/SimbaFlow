using Carter;
using MediatR;
using SimbaFlow.API.Features.Finance.Commands;
using SimbaFlow.API.Features.Finance.Queries;

namespace SimbaFlow.API.Features.Finance;

public class CommissionModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/commissions")
            .WithTags("Commissions")
            .RequireAuthorization();

        group.MapGet("/board", async (
            int? page, int? pageSize, string? status, string? country, string? search,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCommissionBoardQuery(
                page ?? 1, pageSize ?? 50, status, country, search));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Alias for existing UI
        group.MapGet("/", async (
            int? page, int? pageSize, string? status, string? country, string? search,
            ISender sender) =>
        {
            var result = await sender.Send(new GetCommissionBoardQuery(
                page ?? 1, pageSize ?? 50, status, country, search));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/reports/by-partner", async (
            DateOnly? from, DateOnly? to, ISender sender) =>
        {
            var result = await sender.Send(new GetCommissionReportQuery(from, to));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetCommissionByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPut("/{id:guid}/fees", async (Guid id, UpsertFeesRequest body, ISender sender) =>
        {
            var fees = (body.Fees ?? [])
                .Select(f => new FeeLineInput(f.FeeType, f.Description, f.Amount, f.Currency, f.SortOrder))
                .ToList();
            var result = await sender.Send(new UpsertCommissionFeesCommand(id, fees));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/{id:guid}/payments", async (Guid id, RecordPaymentRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordPaymentCommand(
                id, body.Amount, body.Currency, body.Method, body.PaidAt, body.Reference, body.Notes));
            return result.IsSuccess
                ? Results.Json(result, statusCode: result.StatusCode)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/{id:guid}/disputes", async (Guid id, OpenDisputeRequest body, ISender sender) =>
        {
            var result = await sender.Send(new OpenDisputeCommand(id, body.Reason));
            return result.IsSuccess
                ? Results.Json(result, statusCode: result.StatusCode)
                : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/disputes/{disputeId:guid}/resolve", async (
            Guid disputeId, ResolveDisputeRequest body, ISender sender) =>
        {
            var result = await sender.Send(new ResolveDisputeCommand(disputeId, body.ResolutionNotes));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record FeeLineRequest(
    string FeeType,
    string? Description,
    decimal Amount,
    string? Currency,
    int SortOrder = 0);

public record UpsertFeesRequest(List<FeeLineRequest>? Fees);
public record RecordPaymentRequest(
    decimal Amount,
    string? Currency,
    string Method,
    DateTime? PaidAt,
    string? Reference,
    string? Notes);
public record OpenDisputeRequest(string Reason);
public record ResolveDisputeRequest(string ResolutionNotes);
