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

        group.MapGet("/journals", async (
            DateOnly? from, DateOnly? to, string? sourceType, string? search, int? page, int? pageSize,
            ISender sender) =>
        {
            var result = await sender.Send(new GetJournalEntriesQuery(
                from, to, sourceType, search, page ?? 1, pageSize ?? 50));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/reports/trial-balance", async (DateOnly? asOf, ISender sender) =>
        {
            var result = await sender.Send(new GetTrialBalanceQuery(asOf));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/reports/profit-and-loss", async (DateOnly? from, DateOnly? to, ISender sender) =>
        {
            var result = await sender.Send(new GetProfitAndLossQuery(from, to));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapGet("/reports/balance-sheet", async (DateOnly? asOf, ISender sender) =>
        {
            var result = await sender.Send(new GetBalanceSheetQuery(asOf));
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

        group.MapGet("/expenses", async (DateOnly? from, DateOnly? to, string? category, ISender sender) =>
        {
            var result = await sender.Send(new GetExpensesQuery(from, to, category));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/expenses", async (RecordExpenseRequest body, ISender sender) =>
        {
            var result = await sender.Send(new RecordExpenseCommand(
                body.ExpenseDate, body.Category, body.Description, body.Amount, body.Currency,
                body.Method, body.Payee, body.Reference, body.CommissionId));
            return Results.Json(result, statusCode: result.StatusCode);
        });

        group.MapPost("/expenses/{id:guid}/void", async (Guid id, VoidExpenseRequest body, ISender sender) =>
        {
            var result = await sender.Send(new VoidExpenseCommand(id, body.Reason));
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

public record RecordExpenseRequest(
    DateOnly ExpenseDate,
    string Category,
    string Description,
    decimal Amount,
    string? Currency,
    string Method,
    string? Payee,
    string? Reference,
    Guid? CommissionId);

public record VoidExpenseRequest(string Reason);
