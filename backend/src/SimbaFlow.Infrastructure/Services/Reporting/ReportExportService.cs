using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Application.Common.Models;

namespace SimbaFlow.Infrastructure.Services.Reporting;

/// <summary>
/// Renders a generic <see cref="ReportTable"/> to Excel (ClosedXML) or PDF (QuestPDF).
/// Formatting is driven entirely by each column's <see cref="ReportColumnType"/>.
/// </summary>
public class ReportExportService : IReportExportService
{
    private static readonly Color Brand = Color.FromHex("#1B4F9C");
    private static readonly Color HeaderBg = Color.FromHex("#1B4F9C");

    static ReportExportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] ToExcel(ReportTable report)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(SafeSheetName(report.Title));

        // Title + subtitle
        var lastCol = Math.Max(report.Columns.Count, 1);
        sheet.Cell(1, 1).Value = report.Title;
        sheet.Range(1, 1, 1, lastCol).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 15;

        var headerRow = 2;
        if (!string.IsNullOrWhiteSpace(report.Subtitle))
        {
            sheet.Cell(2, 1).Value = report.Subtitle;
            sheet.Range(2, 1, 2, lastCol).Merge();
            sheet.Cell(2, 1).Style.Font.Italic = true;
            sheet.Cell(2, 1).Style.Font.FontColor = XLColor.Gray;
            headerRow = 3;
        }

        // Column headers
        for (var c = 0; c < report.Columns.Count; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = report.Columns[c].Label;
            cell.Style.Font.Bold = true;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4F9C");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Data
        var r = headerRow + 1;
        foreach (var row in report.Rows)
        {
            for (var c = 0; c < report.Columns.Count; c++)
            {
                var col = report.Columns[c];
                var cell = sheet.Cell(r, c + 1);
                var raw = row.GetValueOrDefault(col.Key);
                ApplyExcelValue(cell, col.Type, raw);
            }
            r++;
        }

        sheet.Columns().AdjustToContents();
        if (report.Rows.Count > 0)
            sheet.Range(headerRow, 1, r - 1, lastCol).SetAutoFilter();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public byte[] ToPdf(ReportTable report)
    {
        var generatedAt = report.GeneratedAtUtc ?? DateTime.UtcNow;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily(Fonts.Calibri));

                page.Header().Column(col =>
                {
                    col.Item().Text(report.Title).FontSize(16).Bold().FontColor(Brand);
                    if (!string.IsNullOrWhiteSpace(report.Subtitle))
                        col.Item().Text(report.Subtitle!).FontSize(9).FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(2).Text($"Generated {generatedAt:yyyy-MM-dd HH:mm} UTC")
                        .FontSize(7).FontColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        foreach (var _ in report.Columns)
                            cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var col in report.Columns)
                        {
                            header.Cell()
                                .Background(HeaderBg).Padding(5)
                                .Text(col.Label).FontColor(Colors.White).Bold();
                        }
                    });

                    var rowIndex = 0;
                    foreach (var row in report.Rows)
                    {
                        var bg = rowIndex % 2 == 0 ? "#FFFFFF" : "#F3F6FB";
                        foreach (var col in report.Columns)
                        {
                            var text = FormatValue(col.Type, row.GetValueOrDefault(col.Key));
                            table.Cell().Background(bg).Padding(4)
                                .AlignLeft().Text(text).FontSize(9);
                        }
                        rowIndex++;
                    }

                    if (report.Rows.Count == 0)
                    {
                        table.Cell().ColumnSpan((uint)Math.Max(report.Columns.Count, 1))
                            .Padding(10).AlignCenter()
                            .Text("No data for this report.").FontColor(Colors.Grey.Medium).Italic();
                    }
                });

                page.Footer().AlignRight().Text(t =>
                {
                    t.Span("SimbaFlow · ").FontSize(7).FontColor(Colors.Grey.Medium);
                    t.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                    t.Span(" / ").FontSize(7).FontColor(Colors.Grey.Medium);
                    t.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void ApplyExcelValue(IXLCell cell, ReportColumnType type, object? raw)
    {
        if (raw is null)
        {
            cell.Value = string.Empty;
            return;
        }

        switch (type)
        {
            case ReportColumnType.Number:
                cell.Value = ToDouble(raw);
                cell.Style.NumberFormat.Format = "#,##0";
                break;
            case ReportColumnType.Money:
                cell.Value = ToDouble(raw);
                cell.Style.NumberFormat.Format = "#,##0.00";
                break;
            case ReportColumnType.Percent:
                cell.Value = ToDouble(raw) / 100d;
                cell.Style.NumberFormat.Format = "0.0%";
                break;
            case ReportColumnType.Date:
                if (raw is DateTime dt) { cell.Value = dt; cell.Style.DateFormat.Format = "yyyy-mm-dd"; }
                else if (raw is DateOnly d) { cell.Value = d.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = "yyyy-mm-dd"; }
                else cell.Value = raw.ToString();
                break;
            default:
                cell.Value = raw.ToString();
                break;
        }
    }

    private static string FormatValue(ReportColumnType type, object? raw)
    {
        if (raw is null) return string.Empty;
        return type switch
        {
            ReportColumnType.Number => ToDouble(raw).ToString("#,##0"),
            ReportColumnType.Money => ToDouble(raw).ToString("#,##0.00"),
            ReportColumnType.Percent => ToDouble(raw).ToString("0.0") + "%",
            ReportColumnType.Date => raw switch
            {
                DateTime dt => dt.ToString("yyyy-MM-dd"),
                DateOnly d => d.ToString("yyyy-MM-dd"),
                _ => raw.ToString() ?? string.Empty
            },
            _ => raw.ToString() ?? string.Empty
        };
    }

    private static double ToDouble(object raw) => raw switch
    {
        double d => d,
        decimal m => (double)m,
        int i => i,
        long l => l,
        float f => f,
        _ => double.TryParse(raw.ToString(), out var parsed) ? parsed : 0d
    };

    private static string SafeSheetName(string title)
    {
        var invalid = new[] { '\\', '/', '*', '?', ':', '[', ']' };
        var clean = new string(title.Where(ch => !invalid.Contains(ch)).ToArray());
        return string.IsNullOrWhiteSpace(clean) ? "Report" : clean[..Math.Min(clean.Length, 31)];
    }
}
