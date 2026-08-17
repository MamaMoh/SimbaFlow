namespace SimbaFlow.Application.Common.Models;

/// <summary>
/// Column value type — drives formatting in table, Excel, and PDF output.
/// </summary>
public enum ReportColumnType
{
    Text = 0,
    Number = 1,
    Money = 2,
    Date = 3,
    Percent = 4
}

public record ReportColumn(string Key, string Label, ReportColumnType Type = ReportColumnType.Text);

/// <summary>
/// A generic, self-describing tabular report. Rendered as an on-screen table,
/// a simple chart (when ChartLabelKey/ChartValueKey are set), and Excel/PDF exports.
/// Rows are keyed by <see cref="ReportColumn.Key"/>.
/// </summary>
public record ReportTable(
    string Key,
    string Title,
    string? Subtitle,
    List<ReportColumn> Columns,
    List<Dictionary<string, object?>> Rows,
    string? ChartLabelKey = null,
    string? ChartValueKey = null,
    DateTime? GeneratedAtUtc = null);

/// <summary>One entry in the report catalog (the picker on the Reports page).</summary>
public record ReportCatalogItem(string Key, string Name, string Category, string Description);
