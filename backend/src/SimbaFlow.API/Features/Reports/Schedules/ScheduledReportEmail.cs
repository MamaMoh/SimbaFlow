using System.Globalization;
using System.Net;
using System.Text;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.API.Features.Reports.Schedules;

/// <summary>
/// The email a scheduled report arrives as: the report's first rows in the body, so the common
/// case can be read on a phone without opening anything, and the whole report attached.
/// </summary>
public static class ScheduledReportEmail
{
    /// <summary>Rows shown in the body. The attachment always has all of them.</summary>
    public const int BodyRowLimit = 25;

    public static string Subject(ReportTable report, string? agencyName) =>
        string.IsNullOrWhiteSpace(agencyName)
            ? $"{report.Title} — {DateTime.UtcNow:d MMM yyyy}"
            : $"{report.Title} — {agencyName} — {DateTime.UtcNow:d MMM yyyy}";

    public static string HtmlBody(ReportTable report, string? agencyName, string attachmentName, string scheduleText)
    {
        var e = (string? s) => WebUtility.HtmlEncode(s ?? string.Empty);
        var sb = new StringBuilder();

        sb.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;color:#1f2937\">");
        sb.Append($"<h2 style=\"margin:0 0 4px;font-size:18px\">{e(report.Title)}</h2>");
        if (!string.IsNullOrWhiteSpace(agencyName))
            sb.Append($"<div style=\"color:#6b7280;margin-bottom:4px\">{e(agencyName)}</div>");
        if (!string.IsNullOrWhiteSpace(report.Subtitle))
            sb.Append($"<p style=\"margin:8px 0\">{e(report.Subtitle)}</p>");

        if (report.Rows.Count == 0)
        {
            sb.Append("<p style=\"color:#6b7280\">Nothing to report this time.</p>");
        }
        else
        {
            sb.Append("<table style=\"border-collapse:collapse;margin:12px 0;font-size:13px\">");
            sb.Append("<tr>");
            foreach (var col in report.Columns)
            {
                var align = IsNumeric(col.Type) ? "right" : "left";
                sb.Append($"<th style=\"text-align:{align};padding:6px 10px;border-bottom:2px solid #e5e7eb;background:#f9fafb\">{e(col.Label)}</th>");
            }
            sb.Append("</tr>");

            foreach (var row in report.Rows.Take(BodyRowLimit))
            {
                sb.Append("<tr>");
                foreach (var col in report.Columns)
                {
                    var align = IsNumeric(col.Type) ? "right" : "left";
                    row.TryGetValue(col.Key, out var value);
                    sb.Append($"<td style=\"text-align:{align};padding:6px 10px;border-bottom:1px solid #f3f4f6\">{e(FormatCell(value, col.Type))}</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");

            if (report.Rows.Count > BodyRowLimit)
                sb.Append($"<p style=\"color:#6b7280\">Showing the first {BodyRowLimit} of {report.Rows.Count} rows. The attachment has them all.</p>");
        }

        sb.Append($"<p>The full report is attached as <b>{e(attachmentName)}</b>.</p>");
        sb.Append($"<p style=\"color:#9ca3af;font-size:12px;margin-top:24px\">Sent by SimbaFlow {e(scheduleText)}. Anyone with permission to manage scheduled reports can change or stop it on the Reports page.</p>");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static bool IsNumeric(ReportColumnType type) =>
        type is ReportColumnType.Number or ReportColumnType.Money or ReportColumnType.Percent;

    /// <summary>Same formatting as the on-screen table.</summary>
    public static string FormatCell(object? value, ReportColumnType type)
    {
        if (value is null || value is string { Length: 0 }) return "—";
        var inv = CultureInfo.InvariantCulture;
        try
        {
            return type switch
            {
                ReportColumnType.Number => Convert.ToDecimal(value, inv).ToString("#,0.#", inv),
                ReportColumnType.Money => Convert.ToDecimal(value, inv).ToString("#,0.00", inv),
                ReportColumnType.Percent => Convert.ToDecimal(value, inv).ToString("#,0.#", inv) + "%",
                ReportColumnType.Date => value is DateTime d ? d.ToString("yyyy-MM-dd", inv)
                    : value is DateOnly o ? o.ToString("yyyy-MM-dd", inv)
                    : Convert.ToString(value, inv) ?? "",
                _ => Convert.ToString(value, inv) ?? ""
            };
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return Convert.ToString(value, inv) ?? "";
        }
    }
}
