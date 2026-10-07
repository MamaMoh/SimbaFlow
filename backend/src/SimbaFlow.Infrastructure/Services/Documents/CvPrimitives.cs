using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// The drawing pieces every CV layout is built from.
///
/// The agencies' forms differ in what goes where, not in how a labelled row or a section bar is
/// drawn — so those live here once and each layout arranges them.
/// </summary>
internal static class CvPrimitives
{
    internal static readonly Color Maroon = Color.FromHex("#7A1F2B");
    internal static readonly Color AgencyBlue = Color.FromHex("#1B4F9C");
    internal static readonly Color Navy = Color.FromHex("#12294F");
    internal static readonly Color PanelBlue = Color.FromHex("#D6E4F5");
    internal static readonly Color Border = Color.FromHex("#222222");
    internal static readonly Color LabelBg = Color.FromHex("#F5F3F1");

    /// <summary>
    /// A filled or empty checkbox.
    ///
    /// Drawn rather than typed: the fonts bundled for Latin, Arabic and Ethiopic have no ✓ or ✗,
    /// so those characters print as empty boxes — exactly the failure that made Arabic render as
    /// tofu before. A square that is filled or not needs no glyph at all.
    /// </summary>
    internal static void TickBox(IContainer e, bool on) =>
        e.Width(10).Height(10)
            .Border(0.9f).BorderColor(on ? Navy : Colors.Grey.Medium)
            .Background(on ? Navy : Colors.White);

    internal static void PlaceImage(IContainer e, byte[]? bytes, string placeholder)
    {
        if (bytes is { Length: > 0 })
            e.Padding(3).Image(bytes).FitArea();
        else
            e.Text(placeholder).FontSize(8).FontColor(Colors.Grey.Medium);
    }

    internal static void PlaceFullBodyImage(IContainer e, byte[]? bytes)
    {
        if (bytes is { Length: > 0 })
            e.Padding(2).Image(bytes).FitArea();
        else
            e.Text("FULL PHOTO").FontSize(8).FontColor(Colors.Grey.Medium);
    }


    /// <summary>
    /// A single-column profile sheet.
    ///
    /// The enjaz form exists to satisfy an embassy, and it reads like it — dense bilingual rows
    /// sized for a clerk checking boxes. This is for the other audience: a partner deciding
    /// whether to take someone. Large portrait, plain English headings, and only the facts that
    /// bear on that decision, so it can be read at a glance rather than searched.
    /// </summary>
    internal static void SectionBar(ColumnDescriptor col, string en, string ar)
    {
        col.Item().Background(Maroon).PaddingVertical(FormType.BarPadding).PaddingHorizontal(5).Row(r =>
        {
            r.RelativeItem().Text(en).FontSize(FormType.Heading).Bold().FontColor(Colors.White);
            r.RelativeItem().AlignRight().Text(ar).FontSize(FormType.Heading).Bold().FontColor(Colors.White);
        });
    }

    internal static void BilingualRow(ColumnDescriptor col, string en, string ar, string value) =>
        RowPair(col, en, ar, value, 96);

    internal static void CompactRow(ColumnDescriptor col, string en, string ar, string value) =>
        RowPair(col, en, ar, value, 70);

    /** Label | value only — one content column (no Arabic side column). */
    internal static void SimpleRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(0.4f).BorderColor(Border).Row(r =>
        {
            r.ConstantItem(112).Background(LabelBg).BorderRight(0.4f).BorderColor(Border)
                .PaddingVertical(FormType.RoomyRowPadding).PaddingHorizontal(3)
                .Text(label).FontSize(FormType.Label).SemiBold().FontColor(FormType.LabelColor);

            r.RelativeItem().PaddingVertical(FormType.RoomyRowPadding).PaddingHorizontal(3)
                .AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(FormType.Value).Bold();
        });
    }

    internal static void RowPair(ColumnDescriptor col, string en, string ar, string value, float labelWidth)
    {
        col.Item().BorderBottom(0.4f).BorderColor(Border).Row(r =>
        {
            r.ConstantItem(labelWidth).Background(LabelBg).BorderRight(0.4f).BorderColor(Border)
                .PaddingVertical(FormType.RoomyRowPadding).PaddingHorizontal(3)
                .Text(en).FontSize(FormType.Label).SemiBold().FontColor(FormType.LabelColor);

            r.RelativeItem().PaddingVertical(FormType.RoomyRowPadding).PaddingHorizontal(3)
                .AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(FormType.Value).Bold();

            r.ConstantItem(labelWidth).Background(LabelBg).BorderLeft(0.4f).BorderColor(Border)
                .PaddingVertical(FormType.RoomyRowPadding).PaddingHorizontal(3)
                .AlignRight().Text(ar).FontSize(FormType.Label).SemiBold().FontColor(FormType.LabelColor);
        });
    }

    internal static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    internal static int? AgeYears(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age >= 0 ? age : null;
    }

    internal static string? FormatAddress(Candidate c)
    {
        var parts = new[] { c.HouseNo, c.Woreda, c.Subcity, c.Address, c.City, c.Region, c.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
