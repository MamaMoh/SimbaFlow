using System.Reflection;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// The Saudi consular Application for Visa — the enjaz form the embassy actually takes.
///
/// What was here before was a CV wearing the form's title: our own section bars, our own row
/// widths, the fields we happened to hold. The embassy desk works through a specific sheet in a
/// specific order, and a page that carries the same facts in a different shape gets read line by
/// line instead of at a glance, or handed back.
///
/// So this is the sheet. Two barcodes at the head — visa number left, E number right, which are
/// the two numbers on the page a human transcribes wrong and a scanner does not. The
/// ministry's emblem between the applicant's photograph and the consular heading. Then the grid:
/// every row an English label on the left, the value in the middle and the Arabic label on the
/// right, laid out the way the form lays them out, down to the boxes that stay empty because the
/// consulate fills them in by hand.
///
/// The empty rows are not oversights. Date of arrival, mode of payment, the dependants table, the
/// whole official-use block at the foot — those are written on at the counter, and a form that
/// omits them is not the form.
/// </summary>
internal static class VisaFormLayout
{
    private static readonly Color Ink = Color.FromHex("#000000");
    private static readonly Color Rule = Color.FromHex("#000000");
    private static readonly Color Chosen = Color.FromHex("#9A9A9A");

    private const float Hair = 0.7f;
    private const float Label = 9.5f;
    private const float Value = 9.5f;
    private const float Arabic = 9f;
    private const float RowPad = 3.1f;

    /// <summary>The margin the form is laid out inside, matching the official sheet's.</summary>
    private const float Margin = 21f;

    /// <summary>
    /// The width of one barcode module. 0.95pt is what the official form uses and it is also
    /// about the narrowest a desk scanner reads reliably off a laser print.
    /// </summary>
    private const float Module = 0.95f;

    private const float BarcodeHeight = 27.5f;

    public static void Compose(
        IDocumentContainer container,
        Candidate candidate,
        byte[]? photoBytes,
        AgencyIdentity agency)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(Margin);
            page.DefaultTextStyle(x => x
                .FontFamily(DocumentFonts.Chain)
                .FontSize(Label)
                .LineHeight(1.1f)
                .FontColor(Ink));

            page.Content().Column(root =>
            {
                Head(root, candidate, photoBytes, agency);
                Grid(root, candidate, agency);
            });

            page.Footer().PaddingTop(6).Row(foot =>
            {
                foot.RelativeItem().Text(DateTime.UtcNow.ToString("dddd, MMMM d, yyyy"))
                    .FontSize(8).Bold();
                foot.RelativeItem().AlignRight().Text(Contact(agency)).FontSize(8).Bold();
            });
        });
    }

    private static string Contact(AgencyIdentity agency) =>
        string.Join("  |  ", new[] { agency.Name, agency.Email }
            .Where(s => !string.IsNullOrWhiteSpace(s)));

    // ─────────────────────────────────────────────────────────────────────────────
    // The head: barcodes, photograph, emblem, consular heading
    // ─────────────────────────────────────────────────────────────────────────────

    private static void Head(
        ColumnDescriptor root, Candidate candidate, byte[]? photoBytes, AgencyIdentity agency)
    {
        root.Item().Row(bar =>
        {
            bar.RelativeItem().Column(c =>
            {
                c.Item().AlignLeft().Element(e => Barcode(e, candidate.VisaNumber));
                c.Item().PaddingTop(1).Row(line =>
                {
                    line.AutoItem().PaddingRight(6).AlignBottom().Text("VISA No:").FontSize(6.5f);
                    line.RelativeItem().Text(V(candidate.VisaNumber)).FontSize(11).Bold();
                });
                c.Item().PaddingTop(2).Text(text =>
                {
                    text.Span("Sponsor :  ").FontSize(Value);
                    text.Span(V(candidate.SponsorName)).FontSize(Value);
                });
            });

            bar.RelativeItem().Column(c =>
            {
                // The E number, not the passport number. They sit side by side on the form and
                // look alike — E000111222 against EP0000001 — and the first copy of this read the
                // wrong one off the original. The passport number has its own row in the grid.
                c.Item().AlignCenter().Element(e => Barcode(e, candidate.ENumber));
                c.Item().PaddingTop(1).AlignCenter()
                    .Text(V(candidate.ENumber)).FontSize(11).Bold();
                c.Item().PaddingTop(1).AlignCenter().Text("EMBASSY OF SAUDI ARABIA").FontSize(Value);
                c.Item().AlignCenter().Text("CONSULAR SECTION").FontSize(Value);
            });
        });

        root.Item().PaddingTop(4).Row(band =>
        {
            band.ConstantItem(112).Height(118).Border(Hair).BorderColor(Rule)
                .AlignCenter().AlignMiddle()
                .Element(e => CvPrimitives.PlaceImage(e, photoBytes, "PHOTO"));

            band.RelativeItem().PaddingHorizontal(8).AlignCenter().AlignTop()
                .Height(100).Element(Emblem);

            band.ConstantItem(230).Column(c =>
            {
                c.Item().AlignCenter().Text("سفارة المملكة العربية السعودية").FontSize(10).Bold();
                c.Item().AlignCenter().Text("القسم القنصلي").FontSize(10).Bold();
                c.Item().PaddingTop(14).AlignCenter().Text(agency.Name).FontSize(Value);
                if (!string.IsNullOrWhiteSpace(agency.Email))
                    c.Item().PaddingTop(10).AlignCenter().Text(agency.Email).FontSize(10).Bold();
            });
        });
    }

    /// <summary>
    /// The Ministry of Foreign Affairs emblem that heads the form.
    ///
    /// Carried in the assembly rather than asked of the agency: it is the ministry's mark, the
    /// same on every copy of this form, and a consular sheet without it does not look like one.
    /// A missing resource leaves a gap rather than throwing — the rest of the page is still the
    /// form, and a document that fails to generate helps nobody.
    /// </summary>
    private static void Emblem(IContainer container)
    {
        if (EmblemBytes is { Length: > 0 }) container.Image(EmblemBytes).FitHeight();
        else container.Text(string.Empty);
    }

    private static readonly byte[]? EmblemBytes = LoadEmblem();

    private static byte[]? LoadEmblem()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var name = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith("saudi-mofa-emblem.jpg", StringComparison.Ordinal));
            if (name is null) return null;

            using var stream = assembly.GetManifestResourceStream(name);
            if (stream is null) return null;

            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            return buffer.ToArray();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// One Code 128 symbol, drawn as alternating filled and empty strips.
    ///
    /// Strips rather than a drawn path because every bar is one rectangle of its full width — a
    /// four-module bar is a single item, not four touching ones — so there is no seam for the
    /// renderer to round into a white line through the middle of a bar.
    ///
    /// Nothing is drawn when there is no number yet. A candidate reaches this form before the
    /// visa is issued often enough that an empty space is the honest answer; a barcode of a blank
    /// string would scan as a blank string.
    /// </summary>
    private static void Barcode(IContainer container, string? value)
    {
        var modules = Code128.Modules(value);
        if (modules.Count == 0)
        {
            container.Height(BarcodeHeight);
            return;
        }

        container.Height(BarcodeHeight).Width(Code128.Width(value) * Module).Row(row =>
        {
            row.ConstantItem(Code128.QuietZone * Module);

            var dark = true;
            foreach (var width in modules)
            {
                var strip = row.ConstantItem(width * Module);
                if (dark) strip.Background(Ink);
                dark = !dark;
            }

            row.ConstantItem(Code128.QuietZone * Module);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // The grid
    // ─────────────────────────────────────────────────────────────────────────────

    private static void Grid(ColumnDescriptor root, Candidate candidate, AgencyIdentity agency)
    {
        var nationality = Or(candidate.Nationality, "Ethiopia");

        root.Item().PaddingTop(6).Border(Hair).BorderColor(Rule).Column(table =>
        {
            Full(table, "Full Name :", candidate.FullName.ToUpperInvariant(), "الاسم الكامل");

            Pair(table,
                ("Date of Birth :", candidate.DateOfBirth.ToString("dd/MM/yyyy"), "تاريخ الميلاد"),
                ("Place of Birth :", V(candidate.PlaceOfBirth), "مكان الميلاد"));

            Pair(table,
                ("Past Nationality :", nationality, "الجنسية السابقة"),
                ("Current Nationality :", nationality, "الجنسية الحالية"));

            Pair(table,
                ("Sex :", candidate.Gender.ToString(), "الجنس"),
                ("Marital Status :", V(candidate.MaritalStatus), "الحالة الاجتماعية"));

            Pair(table,
                ("Sect :", "", ""),
                ("Religion :", V(candidate.Religion), "الديانة"));

            Pair(table,
                ("Qualification :", V(candidate.Qualification), "المؤهل العلمي"),
                ("Profession :", V(candidate.Occupation), "المهنة"));

            Banner(table, "Home address and telephone No. :", "عنوان السكن");
            Written(table, HomeAddress(candidate));

            Banner(table, "Business address and telephone No. :", "عنوان العمل ورقم الهاتف");
            Written(table, "");

            Purpose(table, candidate);

            Issue(table, candidate);
            Full(table, "Date of Expiry :", D(candidate.PassportExpiryDate), "تاريخ الانتهاء");

            Triple(table,
                ("", "مدة الاقامه بالمملكة"),
                ("", "تاريخ الوصول"),
                ("", "تاريخ المغادرة"));
            Triple(table,
                ("Duration of stay in the Kingdom :", ""),
                ("Date of arrival :", ""),
                ("Date of departure :", ""));

            Triple(table,
                ("Mode of Payment :", "طريقة الدفع"),
                ("Payment No :", "رقم الدفع"),
                ("Date :", "تاريخ"));

            Banner(table, "Relationship :", "");

            Pair(table,
                ("Destination :", "", "المكان المقصود"),
                ("Dealer Name :", "", "اسم البائع"));

            Banner(table, "Dependents traveling in the same passport:",
                "إيضاحات تخص أفراد العائله المضافين على نفس جواز السفر");
            Dependents(table);

            Banner(table, "Name and address of company or individual in the Kingdom:",
                "أسم وعنوان الشركه او أسم الشخص وعنوانه بالمملكة");
            Written(table, Sponsor(candidate));

            Certification(table, candidate);
            Official(table);
        });
    }

    private static string HomeAddress(Candidate candidate)
    {
        var address = string.Join(", ",
            new[] { candidate.Address, candidate.Subcity, candidate.City, candidate.Country }
                .Where(s => !string.IsNullOrWhiteSpace(s)));

        return Line(address, candidate.PhoneNumber);
    }

    /// <summary>
    /// Several values on one line, each kept where it was put.
    ///
    /// These lines mix scripts — an Arabic sponsor name beside a Latin one, an Arabic city at the
    /// end of a Latin street — and the separators between them are direction-neutral. Left to
    /// itself the bidirectional algorithm reads the neutral runs as part of whichever Arabic
    /// stretch they touch and swings everything around it, so the identity number ends up between
    /// the two halves of the address. Each value goes in a first-strong isolate: inside, it reads
    /// in its own direction; outside, it is one indivisible thing that cannot be reordered.
    /// </summary>
    private static string Line(params string?[] parts) =>
        string.Join("  ·  ", parts
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => $"\u2068{p!.Trim()}\u2069"));

    /// <summary>
    /// Who in the Kingdom the applicant is going to. Everything the contract gave us about the
    /// sponsor, on the line of the form that asks for exactly that.
    /// </summary>
    private static string Sponsor(Candidate candidate) =>
        Line(candidate.SponsorName,
             candidate.SponsorArabicName,
             candidate.SponsorIdNumber,
             candidate.SponsorAddress,
             candidate.SponsorPhone);

    // ─────────────────────────────────────────────────────────────────────────────
    // Row shapes
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Label, value and Arabic label across the full width.</summary>
    private static void Full(ColumnDescriptor table, string label, string value, string arabic) =>
        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.ConstantItem(96).Text(label).FontSize(Label);
            r.RelativeItem().Text(value).FontSize(Value).Bold();
            r.ConstantItem(120).AlignRight().Text(arabic).FontSize(Arabic);
        });

    /// <summary>The same, twice, split down the middle — most of the form is these.</summary>
    private static void Pair(
        ColumnDescriptor table,
        (string Label, string Value, string Arabic) left,
        (string Label, string Value, string Arabic) right) =>
        table.Item().BorderBottom(Hair).BorderColor(Rule).Row(r =>
        {
            r.RelativeItem().BorderRight(Hair).BorderColor(Rule).Element(Pad).Element(e => Half(e, left));
            r.RelativeItem().Element(Pad).Element(e => Half(e, right));
        });

    private static void Half(IContainer container, (string Label, string Value, string Arabic) cell) =>
        container.Row(r =>
        {
            r.ConstantItem(96).Text(cell.Label).FontSize(Label);
            r.RelativeItem().Text(cell.Value).FontSize(Value).Bold();
            r.ConstantItem(78).AlignRight().Text(cell.Arabic).FontSize(Arabic);
        });

    /// <summary>Three cells across, for the rows the form divides into thirds.</summary>
    private static void Triple(
        ColumnDescriptor table,
        (string Label, string Arabic) left,
        (string Label, string Arabic) middle,
        (string Label, string Arabic) right) =>
        table.Item().BorderBottom(Hair).BorderColor(Rule).Row(r =>
        {
            Third(r, left, border: true);
            Third(r, middle, border: true);
            Third(r, right, border: false);
        });

    private static void Third(RowDescriptor row, (string Label, string Arabic) cell, bool border)
    {
        var item = row.RelativeItem();
        if (border) item = item.BorderRight(Hair).BorderColor(Rule);
        item.Element(Pad).Row(r =>
        {
            r.RelativeItem().Text(cell.Label).FontSize(Label);
            r.AutoItem().AlignRight().Text(cell.Arabic).FontSize(Arabic);
        });
    }

    /// <summary>A heading that runs the width, English left and Arabic right, with no value.</summary>
    private static void Banner(ColumnDescriptor table, string label, string arabic) =>
        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.RelativeItem().Text(label).FontSize(Label);
            r.AutoItem().AlignRight().Text(arabic).FontSize(Arabic);
        });

    /// <summary>The line under a heading that a value is written on, indented as the form indents it.</summary>
    private static void Written(ColumnDescriptor table, string value) =>
        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).PaddingLeft(60)
            .MinHeight(14).Text(value).FontSize(Value).Bold();

    private static IContainer Pad(IContainer container) =>
        container.PaddingVertical(RowPad).PaddingHorizontal(5);

    // ─────────────────────────────────────────────────────────────────────────────
    // The rows that are not simply label-and-value
    // ─────────────────────────────────────────────────────────────────────────────

    private static readonly string[] Purposes =
        ["Work", "Transit", "Visit", "Umrah", "Residence", "Hajj", "Diplomacy", "Other"];

    /// <summary>
    /// Purpose of travel: all eight choices printed, the one that applies shaded.
    ///
    /// Shaded rather than ticked because that is how the form marks it, and because none of the
    /// fonts bundled for Latin, Arabic and Ethiopic carries a tick — it would print as an empty
    /// box, which on this row reads as "none of these".
    /// </summary>
    private static void Purpose(ColumnDescriptor table, Candidate candidate)
    {
        var chosen = Purposes.FirstOrDefault(p =>
            string.Equals(p, candidate.VisaType?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? "Work";

        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.ConstantItem(96).AlignMiddle().Text("Purpose Of Travel").FontSize(Label);
            foreach (var purpose in Purposes)
            {
                var cell = r.RelativeItem();
                if (purpose == chosen) cell = cell.Background(Chosen);
                cell.AlignCenter().AlignMiddle().Text(purpose).FontSize(Label);
            }
            r.ConstantItem(34).AlignRight().AlignMiddle().Text("الغرض").FontSize(Arabic);
        });
    }

    /// <summary>Place, date and number of issue — three label/value pairs sharing one row.</summary>
    private static void Issue(ColumnDescriptor table, Candidate candidate)
    {
        var placeOfIssue = Or(candidate.PassportPlaceOfIssue, "Ethiopia").ToUpperInvariant();

        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.ConstantItem(74).Text("Place of Issue :").FontSize(Label);
            r.ConstantItem(68).Text(placeOfIssue).FontSize(Value).Bold();
            r.ConstantItem(54).AlignRight().Text("مكان الاصدار").FontSize(Arabic);

            r.ConstantItem(72).PaddingLeft(6).Text("Date of Issue :").FontSize(Label);
            r.ConstantItem(54).Text(D(candidate.PassportIssueDate)).FontSize(Value).Bold();
            r.ConstantItem(54).AlignRight().Text("تاريخ الاصدار").FontSize(Arabic);

            r.ConstantItem(68).PaddingLeft(6).Text("Passport No :").FontSize(Label);
            r.RelativeItem().Text(candidate.PassportNumber).FontSize(Value).Bold();
            r.ConstantItem(48).AlignRight().Text("رقم الجواز").FontSize(Arabic);
        });
    }

    /// <summary>
    /// The dependants table. Printed empty: dependants travelling on the same passport are
    /// written in at the counter, and the consulate expects the ruled box to write them in.
    /// </summary>
    private static void Dependents(ColumnDescriptor table)
    {
        string[] arabic = ["الاسم الكامل", "الجنس", "تاريخ الميلاد", "صلة"];
        string[] english = ["Full Name", "Sex", "Date of Birth", "Relationship"];

        table.Item().BorderBottom(Hair).BorderColor(Rule).PaddingHorizontal(14).PaddingVertical(3)
            .Border(Hair).BorderColor(Rule).Column(box =>
            {
                box.Item().Row(r =>
                {
                    // Right to left: the Arabic heading of each column sits above the English one,
                    // and the form orders the columns as Arabic reads them.
                    for (var i = arabic.Length - 1; i >= 0; i--)
                        Heading(r, arabic[i], i > 0);
                });
                box.Item().Row(r =>
                {
                    for (var i = english.Length - 1; i >= 0; i--)
                        Heading(r, english[i], i > 0);
                });
            });

        static void Heading(RowDescriptor row, string text, bool border)
        {
            var item = row.RelativeItem();
            if (border) item = item.BorderRight(Hair).BorderColor(Rule);
            item.PaddingVertical(2).AlignCenter().Text(text).FontSize(Arabic);
        }
    }

    private static void Certification(ColumnDescriptor table, Candidate candidate)
    {
        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            // The English sentence is half again as long as the Arabic one, so the halves are not
            // equal — split down the middle it wraps to three lines and pushes the sheet over.
            r.RelativeItem(1.5f).Column(c =>
            {
                c.Item().Text("The undersigned hereby certify that all the information I have provided are correct.")
                    .FontSize(8f).Bold();
                c.Item().Text("I will abide by the laws of the Kingdom during the period of my residence in it.")
                    .FontSize(8f).Bold();
            });
            r.RelativeItem(1f).Column(c =>
            {
                c.Item().AlignRight().Text("قر انا الموقع ادناه بأن كل المعلومات التي دونتها صحيحه")
                    .FontSize(8f).Bold();
                c.Item().AlignRight().Text("وسأكون ملتزما بقوانين المملكة أثناء فترة وجودي بها")
                    .FontSize(8f).Bold();
            });
        });

        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.ConstantItem(96).Text($"Date :  {DateTime.UtcNow:dd/MM/yyyy}").FontSize(Label);
            r.ConstantItem(140).Text("Signature : ____________________").FontSize(Label);
            r.ConstantItem(34).AlignRight().Text("التوقيع").FontSize(Arabic);
            r.RelativeItem().PaddingLeft(8)
                .Text($"Name : {candidate.FullName.ToUpperInvariant()}").FontSize(Label);
            r.ConstantItem(66).AlignRight().Text("الاسم الكامل").FontSize(Arabic);
        });
    }

    /// <summary>
    /// The block the consulate completes. Every line of it stays blank — it is theirs, and the
    /// form is not the form without the space to write in.
    /// </summary>
    private static void Official(ColumnDescriptor table)
    {
        table.Item().BorderBottom(Hair).BorderColor(Rule).Element(Pad).Row(r =>
        {
            r.RelativeItem().Text("For official use only:").FontSize(Label).Bold();
            r.AutoItem().AlignRight().Text("للاستخدام الرسمي فقط").FontSize(Arabic);
        });

        Triple(table, ("Date :", "التاريخ"), ("Authorization :", "تفويض"), ("", ""));
        Banner(table, "Visit / Work For :", "زيارة أو العمل من أجل");
        Triple(table, ("Type", "نوع"), ("Duration :", "المدة الزمنية"), ("", ""));

        table.Item().Element(Pad).Row(r =>
        {
            r.RelativeItem().Column(c =>
            {
                c.Item().Text("رئيس القسم القنصلي").FontSize(Arabic);
                c.Item().Text("Head of consular section").FontSize(Label);
            });
            r.RelativeItem().Column(c =>
            {
                c.Item().AlignRight().Text("فحص بواسطة").FontSize(Arabic);
                c.Item().AlignRight().Text("Checked by").FontSize(Label);
            });
        });
    }

    private static string D(DateOnly? date) => date?.ToString("dd/MM/yyyy") ?? "";

    /// <summary>
    /// The value, or what it is when nobody has said otherwise. Only for the two fields that
    /// have an answer regardless: an Ethiopian agency's candidates are Ethiopian and their
    /// passports are issued in Ethiopia.
    /// </summary>
    private static string Or(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    /// <summary>
    /// A value, or nothing at all.
    ///
    /// Nothing rather than a dash: this is a form, and a blank line on a form means the same
    /// thing to the clerk reading it as it does on the printed original — write it in. A dash is
    /// our software talking about itself.
    /// </summary>
    private static string V(string? value) => value?.Trim() ?? "";
}
