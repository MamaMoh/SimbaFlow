using Carter;
using MediatR;
using SimbaFlow.API.Features.Reports.Queries;

namespace SimbaFlow.API.Features.Reports;

public class ReportModule : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reports")
            .WithTags("Reports")
            .RequireAuthorization();

        // Catalog of available reports
        group.MapGet("/", async (ISender sender) =>
        {
            var result = await sender.Send(new GetReportCatalogQuery());
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // A single report's data (table + chart hints)
        group.MapGet("/{key}", async (string key, ISender sender) =>
        {
            var result = await sender.Send(new GetReportQuery(key));
            return result.IsSuccess ? Results.Ok(result) : Results.Json(result, statusCode: result.StatusCode);
        });

        // Export a report to Excel or PDF
        group.MapGet("/{key}/export", async (string key, string? format, ISender sender) =>
        {
            var result = await sender.Send(new ExportReportQuery(key, format ?? "excel"));
            if (!result.IsSuccess || result.Data is null)
                return Results.Json(result, statusCode: result.StatusCode);
            return Results.File(result.Data.Bytes, result.Data.ContentType, result.Data.FileName);
        });
    }
}
