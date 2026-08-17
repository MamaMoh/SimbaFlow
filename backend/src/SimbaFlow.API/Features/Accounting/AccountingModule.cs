using Carter;
using MediatR;
using SimbaFlow.API.Features.Accounting.Commands;
using SimbaFlow.API.Features.Accounting.Queries;

namespace SimbaFlow.API.Features.Accounting;

public class AccountingModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/accounting")
            .WithTags("Accounting")
            .RequireAuthorization();

        group.MapGet("/accounts", async (bool? activeOnly, ISender sender) =>
        {
            var result = await sender.Send(new GetAccountsQuery(activeOnly ?? true));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/journals/{id:guid}", async (Guid id, ISender sender) =>
        {
            var result = await sender.Send(new GetJournalEntryByIdQuery(id));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/rates", async (
            string? fromCurrency, string? toCurrency, DateOnly? asOf, int? take, ISender sender) =>
        {
            var result = await sender.Send(new GetExchangeRatesQuery(
                fromCurrency, toCurrency, asOf, take ?? 100));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/rates", async (UpsertRateRequest body, ISender sender) =>
        {
            if (!DateOnly.TryParse(body.EffectiveDate, out var date))
                return Results.Json(new { isSuccess = false, error = "EffectiveDate must be yyyy-MM-dd" }, statusCode: 400);

            var result = await sender.Send(new UpsertExchangeRateCommand(
                body.FromCurrency, body.ToCurrency, body.Rate, date, body.Source));
            return result.IsSuccess
                ? Results.Json(result, statusCode: result.StatusCode)
                : Results.Json(result, statusCode: result.StatusCode);
        });
    }
}

public record UpsertRateRequest(
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    string EffectiveDate,
    string? Source);
