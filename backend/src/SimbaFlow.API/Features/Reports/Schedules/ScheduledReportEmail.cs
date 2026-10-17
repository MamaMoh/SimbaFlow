using System.Globalization;
using System.Net;
using System.Text;
using SimbaFlow.Application.Common.Email;
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

        if (!string.IsNullOrWhiteSpace(agencyName))
            sb.Append(EmailLayout.Paragraph($"Here is <b>{e(report.Title)}</b> for {e(agencyName)}."));
        else
            sb.Append(EmailLayout.Paragraph($"Here is <b>{e(report.Title)}</b>."));
        if (!string.IsNullOrWhiteSpace(report.Subtitle))
            sb.Append(EmailLayout.SmallPrint(e(report.Subtitle)));

        if (report.Rows.Count == 0)
        {
            sb.Append(EmailLayout.Paragraph("Nothing to report this time."));
        }
        else
        {
            sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" style=\"border-collapse:collapse;margin:6px 0 14px;font-size:13px;font-family:Segoe UI,Helvetica,Arial,sans-serif;\">");
            sb.Append("<tr>");
            foreach (var col in report.Columns)
            {
                var align = IsNumeric(col.Type) ? "right" : "left";
                sb.Append($"<th style=\"text-align:{align};padding:7px 8px;border-bottom:2px solid #d9e3dd;background:#f4f8f5;color:#2b3631;\">{e(col.Label)}</th>");
            }
            sb.Append("</tr>");

            foreach (var row in report.Rows.Take(BodyRowLimit))
            {
                sb.Append("<tr>");
                foreach (var col in report.Columns)
                {
                    var align = IsNumeric(col.Type) ? "right" : "left";
                    row.TryGetValue(col.Key, out var value);
                    sb.Append($"<td style=\"text-align:{align};padding:7px 8px;border-bottom:1px solid #eef1ef;color:#2b3631;\">{e(FormatCell(value, col.Type))}</td>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");

            if (report.Rows.Count > BodyRowLimit)
                sb.Append(EmailLayout.SmallPrint($"Showing the first {BodyRowLimit} of {report.Rows.Count} rows. The attachment has them all."));
        }

        sb.Append(EmailLayout.Paragraph($"The full report is attached as <b>{e(attachmentName)}</b>."));

        return EmailLayout.Render(new EmailContent(
            Kicker: "Scheduled report",
            Title: report.Title,
            BodyHtml: sb.ToString(),
            NoteHtml: $"Sent by SimbaFlow {e(scheduleText)}. Anyone with permission to manage scheduled "
                + "reports can change or stop it on the Reports page."));
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
