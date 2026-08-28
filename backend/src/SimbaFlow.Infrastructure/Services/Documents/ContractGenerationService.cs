using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// Renders the MoLS Standard Employment Contract.
///
/// The clause wording lives in EmploymentContractText and is reproduced verbatim; this class only
/// lays it out and substitutes the per-candidate values. English is set left-to-right on the left
/// and the Arabic clause heading right-to-left on the right, mirroring the approved contract, and
/// clauses flow across pages rather than being pinned to fixed page numbers so a longer party block
/// cannot push text off the page.
/// </summary>
public sealed class ContractGenerationService : IContractGenerationService
{
    private static readonly Color Ink = Color.FromHex("#111111");
    private static readonly Color Rule = Color.FromHex("#333333");
    private static readonly Color LabelBg = Color.FromHex("#F2F2F2");
    private static readonly string FontFamily = ResolveFontFamily();

    public Task<byte[]> GenerateAsync(
        Candidate candidate, ContractParties parties, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        static string D(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? "—";

        var signedOn = candidate.ContractDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var cityOfWork = V(candidate.WorksIn ?? candidate.CountryOfTravel);
        var position = V(candidate.Occupation ?? "House Maid");
        var wage = V(candidate.MonthlySalary);
        var period = V(candidate.ContractPeriod ?? "Two years");

        string Fill(string s) => s
            .Replace("{CITY_OF_WORK}", cityOfWork)
            .Replace("{POSITION}", position)
            .Replace("{WAGE}", wage)
            .Replace("{CONTRACT_PERIOD}", period);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontFamily(FontFamily).FontSize(8.5f).FontColor(Ink));

                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text("Foreign MOL · وزارة العمل الأجنبية").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                    r.RelativeItem().AlignCenter().Text(t =>
                    {
                        t.CurrentPageNumber().FontSize(7);
                        t.Span(" / ").FontSize(7);
                        t.TotalPages().FontSize(7);
                    });
                    r.RelativeItem().AlignRight().Text("Foreign Embassy · السفارة الأجنبية").FontSize(6.5f).FontColor(Colors.Grey.Darken1);
                });

                page.Content().Column(root =>
                {
                    // ── Title block ──
                    root.Item().AlignCenter().Text(EmploymentContractText.TitleEn)
                        .FontSize(12).Bold();
                    root.Item().PaddingTop(2).AlignCenter().Text(EmploymentContractText.TitleAr)
                        .FontSize(11).Bold();

                    root.Item().PaddingTop(8).Row(r =>
                    {
                        r.RelativeItem().Text($"CONTRACT # {V(candidate.ContractNo)}").FontSize(9).Bold();
                        r.RelativeItem().AlignRight().Text($"VISA NUMBER # {V(candidate.VisaNumber)}").FontSize(9).Bold();
                    });

                    root.Item().PaddingTop(6).Text(
                        $"This Contract is entered into on {signedOn:dddd}, corresponding to ({signedOn:dd/MM/yyyy}), by and between:")
                        .FontSize(8.5f);

                    // ── A. Employer / Saudi agency ──
                    root.Item().PaddingTop(8).Border(0.7f).BorderColor(Rule).Column(box =>
                    {
                        Bar(box, "A. Hereinafter called the Employer — represented in the Kingdom of Saudi Arabia by Saudi Recruiting Agency",
                            "أ. ويشار إليه فيما يلي بصاحب العمل وتمثله وكالة الاستقدام السعودية");
                        Field(box, "Name", "الاسم", V(parties.SaudiAgencyName));
                        Field(box, "License No", "رقم الترخيص", V(parties.SaudiLicenseNo));
                        Field(box, "Telephone", "رقم الهاتف", V(parties.SaudiPhone));
                        Field(box, "Street", "الشارع", V(parties.SaudiAddress));
                        Field(box, "City", "المدينة", V(parties.SaudiCity));
                        Field(box, "Email", "البريد الإلكتروني", V(parties.SaudiEmail));
                    });

                    // ── B. Domestic Service Worker ──
                    root.Item().PaddingTop(8).Border(0.7f).BorderColor(Rule).Column(box =>
                    {
                        Bar(box, "B. Domestic Service Worker", "ب. العامل المنزلي / العاملة المنزلية");
                        Field(box, "Name", "الاسم", candidate.FullName.ToUpperInvariant());
                        Field(box, "Position", "الوظيفة", position);
                        Field(box, "Address", "العنوان",
                            V(string.Join(", ", new[] { candidate.Address, candidate.City, candidate.Region }
                                .Where(s => !string.IsNullOrWhiteSpace(s)))));
                        Field(box, "Civil Status", "الحالة الاجتماعية", V(candidate.MaritalStatus));
                        Field(box, "Contact No", "رقم الاتصال", V(candidate.PhoneNumber));
                        Field(box, "Passport No", "رقم الجواز", candidate.PassportNumber);
                        Field(box, "Place of Issue", "مكان إصداره", V(candidate.PassportPlaceOfIssue));
                        Field(box, "Date of Issue", "تاريخ إصداره", D(candidate.PassportIssueDate));
                        Field(box, "Next of kin", "أقرب الأقربين", V(candidate.RelativeName));
                        Field(box, "Relationship", "العلاقة", V(candidate.RelativeKinship));
                        Field(box, "Contact No", "رقم الاتصال", V(candidate.RelativePhone));
                        Field(box, "Address", "العنوان",
                            V(string.Join(", ", new[] { candidate.RelativeCity, candidate.RelativeRegion }
                                .Where(s => !string.IsNullOrWhiteSpace(s)))));
                    });

                    // ── Ethiopian recruitment agency ──
                    root.Item().PaddingTop(8).Border(0.7f).BorderColor(Rule).Column(box =>
                    {
                        Bar(box, "Hereinafter called DSW — represented in his/her country by Recruitment Agency",
                            "ويسمى فيما يلي بالعامل المنزلي وتمثله في بلده وكالة الاستقدام");
                        Field(box, "Name", "الاسم", V(parties.EthiopianAgencyName));
                        Field(box, "License No", "رقم الترخيص", V(parties.EthiopianLicenseNo));
                        Field(box, "Street", "الشارع", V(parties.EthiopianAddress));
                        Field(box, "City", "المدينة", V(parties.EthiopianCity));
                        Field(box, "Contact No", "رقم الاتصال", V(parties.EthiopianPhone));
                        Field(box, "Email", "البريد الإلكتروني", V(parties.EthiopianEmail));
                    });

                    root.Item().PaddingTop(10).Text(EmploymentContractText.BindingEn).FontSize(8.5f).Bold();
                    root.Item().PaddingTop(1).AlignRight().Text(EmploymentContractText.BindingAr).FontSize(8.5f).Bold();

                    // ── Clauses ──
                    foreach (var clause in EmploymentContractText.Clauses)
                    {
                        root.Item().PaddingTop(9).Row(r =>
                        {
                            r.RelativeItem().Text($"{clause.Number}. {clause.TitleEn}.").FontSize(9).Bold();
                            r.RelativeItem().AlignRight().Text($"{clause.TitleAr} {clause.Number}").FontSize(9).Bold();
                        });

                        foreach (var line in clause.BodyEn)
                        {
                            var text = Fill(line);
                            // Indented sub-bullets keep their hanging indent.
                            var indent = text.StartsWith("   -") ? 14 : 0;
                            root.Item().PaddingTop(2).PaddingLeft(indent)
                                .Text(text.TrimStart()).FontSize(8).LineHeight(1.25f);
                        }
                    }

                    // ── Signatures ──
                    root.Item().PaddingTop(18).Text("Signatures: · التوقيعات").FontSize(10).Bold();
                    root.Item().PaddingTop(10).Row(r =>
                    {
                        SignatureSlot(r, "Domestic Service Worker", "العامل المنزلي");
                        SignatureSlot(r, "Employer", "صاحب العمل");
                    });
                    root.Item().PaddingTop(16).Row(r =>
                    {
                        SignatureSlot(r, "Saudi Recruitment Agency", "وكالة الاستقدام السعودية");
                        SignatureSlot(r, "Foreign Recruitment Agency", "وكالة الاستقدام الأجنبية");
                    });
                });
            });
        });

        return Task.FromResult(document.GeneratePdf());
    }

    private static void Bar(ColumnDescriptor col, string en, string ar)
    {
        col.Item().Background(LabelBg).BorderBottom(0.5f).BorderColor(Rule)
            .PaddingVertical(3).PaddingHorizontal(5).Column(c =>
            {
                c.Item().Text(en).FontSize(7.5f).Bold();
                c.Item().AlignRight().Text(ar).FontSize(7.5f).Bold();
            });
    }

    private static void Field(ColumnDescriptor col, string en, string ar, string value)
    {
        col.Item().BorderBottom(0.35f).BorderColor(Colors.Grey.Lighten1).Row(r =>
        {
            r.ConstantItem(85).PaddingVertical(2).PaddingHorizontal(4)
                .Text(en).FontSize(7).FontColor(Colors.Grey.Darken3);
            r.RelativeItem().PaddingVertical(2).Text(value).FontSize(8).Bold();
            r.ConstantItem(95).PaddingVertical(2).PaddingHorizontal(4)
                .AlignRight().Text(ar).FontSize(7).FontColor(Colors.Grey.Darken3);
        });
    }

    private static void SignatureSlot(RowDescriptor r, string en, string ar)
    {
        r.RelativeItem().PaddingHorizontal(6).Column(c =>
        {
            c.Item().PaddingTop(18).BorderBottom(0.7f).BorderColor(Rule);
            c.Item().PaddingTop(3).Text(en).FontSize(7.5f);
            c.Item().Text(ar).FontSize(7.5f).FontColor(Colors.Grey.Darken2);
        });
    }

    /// <summary>Arabic needs a font that actually carries the glyphs; fall back through the usual paths.</summary>
    private static string ResolveFontFamily()
    {
        var candidates = new[]
        {
            "/System/Library/Fonts/Supplemental/Arial Unicode.ttf",
            "/Library/Fonts/Arial Unicode.ttf",
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/noto/NotoNaskhArabic-Regular.ttf",
            "C:/Windows/Fonts/arialuni.ttf",
            "C:/Windows/Fonts/arial.ttf"
        };

        const string customName = "SimbaFlowContractFont";
        foreach (var path in candidates)
        {
            if (!File.Exists(path)) continue;
            try
            {
                using var stream = File.OpenRead(path);
                QuestPDF.Drawing.FontManager.RegisterFontWithCustomName(customName, stream);
                return customName;
            }
            catch
            {
                // try the next candidate
            }
        }

        return Fonts.Calibri;
    }
}
