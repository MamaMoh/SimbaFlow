using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Renders EasyEnjaz-style Application for Employment CV (bilingual table layout).
/// </summary>
public class CvGenerationService : ICvGenerationService
{
    private readonly IDocumentBrandingService _branding;

    public CvGenerationService(IDocumentBrandingService branding)
    {
        _branding = branding;
    }

    private static readonly Color Maroon = Color.FromHex("#7A1F2B");
    private static readonly Color AgencyBlue = Color.FromHex("#1B4F9C");
    private static readonly Color Border = Color.FromHex("#222222");
    private static readonly Color LabelBg = Color.FromHex("#F5F3F1");

    static CvGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<byte[]> GenerateAsync(
        Candidate candidate,
        byte[]? photoBytes = null,
        byte[]? fullPhotoBytes = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // The partner's letterhead when the candidate is placed with one, otherwise the agency's.
        var logoBytes = await _branding.GetHeaderLogoAsync(candidate, cancellationToken);
        var template = await _branding.GetCvTemplateAsync(cancellationToken);

        return template == CvTemplates.Profile
            ? RenderProfile(candidate, photoBytes, fullPhotoBytes, logoBytes)
            : RenderEnjaz(candidate, photoBytes, fullPhotoBytes, logoBytes);
    }

    private static byte[] RenderEnjaz(
        Candidate candidate, byte[]? photoBytes, byte[]? fullPhotoBytes, byte[]? logoBytes)
    {

        var agency = string.IsNullOrWhiteSpace(candidate.PartnerName)
            ? "SIMBAFLOW FOREIGN EMPLOYMENT AGENCY"
            : candidate.PartnerName.ToUpperInvariant();

        var age = AgeYears(candidate.DateOfBirth);
        var dob = candidate.DateOfBirth.ToString("dd/MM/yyyy");
        var address = FormatAddress(candidate) ?? "";
        var refNo = candidate.ReferenceNo ?? candidate.LabourId ?? candidate.ApplicationNo ?? candidate.PassportNumber;
        string YesNo(bool v) => v ? "YES" : "—";

        var placeOfBirth = !string.IsNullOrWhiteSpace(candidate.PlaceOfBirth)
            ? candidate.PlaceOfBirth.ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(candidate.City) ? candidate.City.ToUpperInvariant() : "—");

        var passportPlace = !string.IsNullOrWhiteSpace(candidate.PassportPlaceOfIssue)
            ? candidate.PassportPlaceOfIssue.ToUpperInvariant()
            : (!string.IsNullOrWhiteSpace(candidate.Nationality)
                ? candidate.Nationality.ToUpperInvariant()
                : "ETHIOPIA");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12);
                page.DefaultTextStyle(x => x.FontFamily(Services.Documents.DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

                page.Content().Column(root =>
                {
                    // A letterhead if one has been uploaded; the printed name is what we fall
                    // back to, so a document is never blocked on missing branding.
                    if (logoBytes is { Length: > 0 })
                    {
                        // FitArea rather than FitWidth: a wide banner then fills most of the
                        // page as a letterhead should, while a square logo is capped instead of
                        // being blown up to the full width of the sheet.
                        root.Item().AlignCenter().MaxHeight(95).Image(logoBytes).FitArea();
                    }
                    else
                    {
                        root.Item().AlignCenter().Column(h =>
                        {
                            h.Item().AlignCenter().Text(agency)
                                .FontSize(11).Bold().FontColor(AgencyBlue);
                            h.Item().AlignCenter()
                                .Text("وكالة توظيف عمالة أجنبية")
                                .FontSize(9).FontColor(AgencyBlue);
                        });
                    }

                    root.Item().PaddingTop(4).Background(Maroon).PaddingVertical(4).PaddingHorizontal(6).Row(r =>
                    {
                        r.RelativeItem().Text("Application for Employment")
                            .FontSize(10).Bold().FontColor(Colors.White);
                        r.RelativeItem().AlignRight().Text("استمارة توظيف")
                            .FontSize(10).Bold().FontColor(Colors.White);
                    });

                    // Top: fields + portrait
                    root.Item().Border(0.75f).BorderColor(Border).Row(top =>
                    {
                        top.RelativeItem().Column(left =>
                        {
                            BilingualRow(left, "Reference No.", "رقم المرجع", refNo);
                            BilingualRow(left, "Post Applied For", "الوظيفة المطلوبة", candidate.Occupation ?? "—");
                            BilingualRow(left, "Monthly Salary", "الراتب الشهري", candidate.MonthlySalary ?? "—");
                            BilingualRow(left, "Contract Period", "مدة العقد", candidate.ContractPeriod ?? "2 Years");
                            BilingualRow(left, "Phone No.", "رقم الهاتف", candidate.PhoneNumber ?? "—");
                            left.Item().PaddingVertical(5).PaddingHorizontal(4)
                                .AlignCenter().Text(candidate.FullName.ToUpperInvariant())
                                .FontSize(11).Bold();
                        });

                        top.ConstantItem(105).BorderLeft(0.75f).BorderColor(Border)
                            .Background(Colors.Grey.Lighten4)
                            .Height(118)
                            .AlignCenter().AlignMiddle().Element(e => PlaceImage(e, photoBytes, "PHOTO"));
                    });

                    root.Item().PaddingTop(4);

                    // Top pair: applicant details + languages | passport
                    root.Item().Row(topPair =>
                    {
                        topPair.RelativeItem().Column(left =>
                        {
                            left.Item().Border(0.75f).BorderColor(Border).Column(box =>
                            {
                                SectionBar(box, "Details of Applicant", "بيانات مقدم الطلب");
                                BilingualRow(box, "Nationality", "الجنسية", candidate.Nationality ?? "Ethiopia");
                                BilingualRow(box, "Religion", "الديانة", candidate.Religion ?? "—");
                                BilingualRow(box, "Date of Birth", "تاريخ الميلاد", dob);
                                BilingualRow(box, "Place of Birth", "مكان الميلاد", placeOfBirth);
                                BilingualRow(box, "Age", "العمر", age?.ToString() ?? "—");
                                BilingualRow(box, "Address", "العنوان",
                                    string.IsNullOrWhiteSpace(address) ? "—" : Truncate(address, 48));
                                BilingualRow(box, "Marital Status", "الحالة الاجتماعية", candidate.MaritalStatus ?? "—");
                                BilingualRow(box, "No. of Children", "عدد الأطفال",
                                    candidate.NumberOfChildren?.ToString() ?? "—");
                                BilingualRow(box, "Height", "الطول", candidate.Height ?? "—");
                                BilingualRow(box, "Weight", "الوزن", candidate.Weight ?? "—");
                            });

                            left.Item().PaddingTop(3).Border(0.75f).BorderColor(Border).Column(box =>
                            {
                                SectionBar(box, "Languages & Education", "اللغة والتعليم");
                                BilingualRow(box, "English", "الإنجليزية", candidate.EnglishLevel ?? "—");
                                BilingualRow(box, "Arabic", "العربية", candidate.ArabicLevel ?? "—");
                                if (!string.IsNullOrWhiteSpace(candidate.OtherLanguages))
                                {
                                    foreach (var part in candidate.OtherLanguages.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                                    {
                                        var pair = part.Split(':', 2, StringSplitOptions.TrimEntries);
                                        if (pair.Length == 2)
                                            BilingualRow(box, pair[0], pair[0], pair[1]);
                                        else
                                            BilingualRow(box, "Other", "أخرى", part);
                                    }
                                }
                                BilingualRow(box, "Education", "التعليم", candidate.Qualification ?? "—");
                            });
                        });

                        topPair.ConstantItem(4);

                        // AlignTop so the border hugs the 4 passport rows (Row otherwise stretches it).
                        topPair.ConstantItem(190).AlignTop().Border(0.75f).BorderColor(Border).Column(box =>
                        {
                            SectionBar(box, "Passport Detail", "تفاصيل جواز السفر");
                            CompactRow(box, "Passport No.", "رقم الجواز", candidate.PassportNumber);
                            CompactRow(box, "Issue Date", "تاريخ الإصدار",
                                candidate.PassportIssueDate?.ToString("dd/MM/yyyy") ?? "—");
                            CompactRow(box, "Place of Issue", "مكان الإصدار", passportPlace);
                            CompactRow(box, "Expiry Date", "تاريخ الانتهاء",
                                candidate.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "—");
                        });
                    });

                    // Left: Work + Skills (one column). Right: full-body photo (fixed height — no ExtendVertical).
                    root.Item().PaddingTop(3).Row(bottom =>
                    {
                        bottom.RelativeItem().Column(left =>
                        {
                            left.Item().Border(0.75f).BorderColor(Border).Column(box =>
                            {
                                SectionBar(box, "Work Experience", "خبرة العمل");
                                // Single label+value column (no EN | value | AR stretch)
                                SimpleRow(box, "Period",
                                    candidate.ExperienceAbroadYears.HasValue
                                        ? $"{candidate.ExperienceAbroadYears} Year(s)"
                                        : "—");
                                SimpleRow(box, "Country",
                                    candidate.WorksIn ?? candidate.CountryOfTravel ?? "—");
                            });

                            left.Item().PaddingTop(3).Border(0.75f).BorderColor(Border).Column(box =>
                            {
                                SectionBar(box, "Skills & Experience", "المهارات والخبرات");
                                BilingualRow(box, "Cleaning", "التنظيف", YesNo(candidate.SkillCleaning));
                                BilingualRow(box, "Washing", "الغسيل", YesNo(candidate.SkillWashing));
                                BilingualRow(box, "Cooking", "الطبخ",
                                    !string.IsNullOrWhiteSpace(candidate.CookingLevel)
                                        ? candidate.CookingLevel
                                        : YesNo(candidate.SkillCooking));
                                BilingualRow(box, "Baby Sitting", "مجالسة الأطفال",
                                    YesNo(candidate.SkillBabysitting || candidate.SkillChildCare));
                            });
                        });

                        bottom.ConstantItem(4);

                        bottom.ConstantItem(190).AlignTop()
                            .Height(168)
                            .Border(0.75f).BorderColor(Border)
                            .Background(Colors.Grey.Lighten4)
                            .AlignCenter().AlignMiddle()
                            .Element(e => PlaceFullBodyImage(e,
                                fullPhotoBytes is { Length: > 0 } ? fullPhotoBytes : photoBytes));
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> GenerateVisaFormAsync(
        Candidate candidate, byte[]? photoBytes = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var logoBytes = await _branding.GetHeaderLogoAsync(candidate, cancellationToken);
        var document = Document.Create(container => ComposeVisaPage(container, candidate, photoBytes, logoBytes));
        return document.GeneratePdf();
    }

    /// <summary>
    /// One document, one enjaze per page.
    ///
    /// A zip of separate PDFs is fine for filing but useless at a printer — the embassy run is
    /// printed as a batch, so the batch has to be a single document.
    /// </summary>
    public async Task<byte[]> GenerateVisaFormsAsync(
        IReadOnlyList<(Candidate Candidate, byte[]? Photo)> entries,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Each candidate can sit with a different partner, so the letterhead is resolved per page
        // rather than once for the batch.
        var logos = new List<byte[]?>(entries.Count);
        foreach (var (candidate, _) in entries)
            logos.Add(await _branding.GetHeaderLogoAsync(candidate, cancellationToken));

        var document = Document.Create(container =>
        {
            for (var i = 0; i < entries.Count; i++)
                ComposeVisaPage(container, entries[i].Candidate, entries[i].Photo, logos[i]);
        });
        return document.GeneratePdf();
    }

    private static void ComposeVisaPage(
        IDocumentContainer container, Candidate candidate, byte[]? photoBytes, byte[]? logoBytes)
    {

        // Modelled on the Saudi Embassy visa / enjaze form the agencies already circulate: a single
        // bilingual page with the applicant, passport, visa and sponsor blocks, a purpose-of-travel
        // line and a certification with a signature slot.
        static string D(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? "—";
        static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

        var agency = string.IsNullOrWhiteSpace(candidate.PartnerName)
            ? "FOREIGN EMPLOYMENT AGENCY"
            : candidate.PartnerName.ToUpperInvariant();

        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(22);
            page.DefaultTextStyle(x => x.FontFamily(Services.Documents.DocumentFonts.Chain).FontSize(8.5f).FontColor(Colors.Black));

                page.Content().Column(root =>
                {
                    root.Item().Row(head =>
                    {
                        head.RelativeItem().Column(c =>
                        {
                            // The uploaded letterhead stands in for the typed agency name.
                            if (logoBytes is { Length: > 0 })
                                c.Item().AlignLeft().MaxHeight(40).Image(logoBytes).FitHeight();
                            else
                                c.Item().Text(agency).FontSize(12).Bold().FontColor(AgencyBlue);

                            c.Item().Text("Embassy of Saudi Arabia · Consular Section").FontSize(8);
                            c.Item().Text("سفارة المملكة العربية السعودية · القسم القنصلي").FontSize(8);
                        });
                        head.ConstantItem(60).Column(c =>
                        {
                            c.Item().AlignRight().Text("Visa No. / رقم التأشيرة").FontSize(6.5f).FontColor(Colors.Grey.Darken2);
                            c.Item().AlignRight().Text(V(candidate.VisaNumber)).FontSize(9).Bold();
                        });
                    });

                    root.Item().PaddingTop(6).AlignCenter()
                        .Text("VISA APPLICATION · طلب تأشيرة").FontSize(11).Bold();

                    // Applicant + photo
                    root.Item().PaddingTop(10).Row(r =>
                    {
                        r.RelativeItem().Border(0.8f).BorderColor(Border).Column(box =>
                        {
                            SectionBar(box, "Applicant", "مقدم الطلب");
                            BilingualRow(box, "Full Name", "الاسم الكامل", candidate.FullName.ToUpperInvariant());
                            BilingualRow(box, "Date of Birth", "تاريخ الميلاد", candidate.DateOfBirth.ToString("dd/MM/yyyy"));
                            BilingualRow(box, "Place of Birth", "مكان الميلاد", V(candidate.PlaceOfBirth));
                            BilingualRow(box, "Nationality", "الجنسية", V(candidate.Nationality));
                            BilingualRow(box, "Sex", "الجنس", candidate.Gender.ToString());
                            BilingualRow(box, "Marital Status", "الحالة الاجتماعية", V(candidate.MaritalStatus));
                            BilingualRow(box, "Religion", "الديانة", V(candidate.Religion));
                            BilingualRow(box, "Qualification", "المؤهل العلمي", V(candidate.Qualification));
                            BilingualRow(box, "Profession", "المهنة", V(candidate.Occupation));
                            BilingualRow(box, "Home Address", "عنوان السكن",
                                Truncate(string.Join(", ", new[] { candidate.Address, candidate.City }.Where(s => !string.IsNullOrWhiteSpace(s))), 60));
                        });

                        r.ConstantItem(110).PaddingLeft(8).Border(0.8f).BorderColor(Border)
                            .Height(150).Background(Colors.Grey.Lighten4)
                            .AlignCenter().AlignMiddle()
                            .Element(e => PlaceImage(e, photoBytes, "PHOTO"));
                    });

                    // Passport
                    root.Item().PaddingTop(8).Border(0.8f).BorderColor(Border).Column(box =>
                    {
                        SectionBar(box, "Passport", "الجواز");
                        box.Item().Row(r =>
                        {
                            r.RelativeItem().Column(c => {
                                BilingualRow(c, "Passport No.", "رقم الجواز", candidate.PassportNumber);
                                BilingualRow(c, "Place of Issue", "مكان الإصدار", V(candidate.PassportPlaceOfIssue));
                            });
                            r.RelativeItem().Column(c => {
                                BilingualRow(c, "Date of Issue", "تاريخ الإصدار", D(candidate.PassportIssueDate));
                                BilingualRow(c, "Date of Expiry", "تاريخ الانتهاء", D(candidate.PassportExpiryDate));
                            });
                        });
                    });

                    // Visa + purpose
                    root.Item().PaddingTop(8).Border(0.8f).BorderColor(Border).Column(box =>
                    {
                        SectionBar(box, "Visa", "التأشيرة");
                        BilingualRow(box, "Visa No.", "رقم التأشيرة", V(candidate.VisaNumber));
                        BilingualRow(box, "Visa Type", "نوع التأشيرة", V(candidate.VisaType) == "—" ? "Work" : candidate.VisaType!);
                        BilingualRow(box, "Destination", "دولة العمل", V(candidate.CountryOfTravel));
                        BilingualRow(box, "Purpose of Travel", "الغرض من السفر", "Work · عمل");
                    });

                    // Sponsor
                    root.Item().PaddingTop(8).Border(0.8f).BorderColor(Border).Column(box =>
                    {
                        SectionBar(box, "Sponsor", "الكفيل");
                        BilingualRow(box, "Sponsor Name", "اسم الكفيل", V(candidate.SponsorName));
                        BilingualRow(box, "Sponsor (Arabic)", "اسم الكفيل عربي", V(candidate.SponsorArabicName));
                        BilingualRow(box, "Sponsor ID", "رقم الكفيل", V(candidate.SponsorIdNumber));
                        BilingualRow(box, "Sponsor Phone", "هاتف الكفيل", V(candidate.SponsorPhone));
                        BilingualRow(box, "Agent", "الوكيل", V(candidate.AgentName));
                    });

                    // Certification + signature
                    root.Item().PaddingTop(10)
                        .Text("The undersigned certifies that all information provided is correct and undertakes to abide by the laws of the Kingdom during the period of residence.")
                        .FontSize(7.5f).Italic();
                    root.Item().PaddingTop(2).AlignRight()
                        .Text("أقر أدناه بأن كل المعلومات صحيحة وسألتزم بقوانين المملكة أثناء فترة إقامتي بها.")
                        .FontSize(7.5f).Italic();

                    root.Item().PaddingTop(16).Row(r =>
                    {
                        r.RelativeItem().Column(c => {
                            c.Item().Text($"Date / التاريخ: {DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                        });
                        r.RelativeItem().AlignRight().Column(c => {
                            c.Item().Text("Signature / التوقيع: ______________").FontSize(8);
                            c.Item().PaddingTop(2).Text(candidate.FullName.ToUpperInvariant()).FontSize(8).Bold();
                        });
                    });
                });
        });
    }

    /// <summary>Fill the work/skills-height slot; prefer height so portrait full-body photos use the space.</summary>
    private static void PlaceImage(IContainer e, byte[]? bytes, string placeholder)
    {
        if (bytes is { Length: > 0 })
            e.Padding(3).Image(bytes).FitArea();
        else
            e.Text(placeholder).FontSize(8).FontColor(Colors.Grey.Medium);
    }

    private static void PlaceFullBodyImage(IContainer e, byte[]? bytes)
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
    private static byte[] RenderProfile(
        Candidate candidate, byte[]? photoBytes, byte[]? fullPhotoBytes, byte[]? logoBytes)
    {
        string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s.Trim();
        var age = AgeYears(candidate.DateOfBirth);

        var skills = new[]
        {
            candidate.SkillCleaning ? "Cleaning" : null,
            candidate.SkillWashing ? "Washing" : null,
            candidate.SkillCooking ? "Cooking" : null,
            candidate.SkillBabysitting ? "Baby sitting" : null,
            candidate.SkillChildCare ? "Child care" : null,
            candidate.SkillIroning ? "Ironing" : null,
            candidate.SkillSewing ? "Sewing" : null,
        }.Where(s => s is not null).ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(28);
                page.DefaultTextStyle(x => x
                    .FontFamily(Services.Documents.DocumentFonts.Chain)
                    .FontSize(9.5f).FontColor(Colors.Black));

                page.Content().Column(root =>
                {
                    if (logoBytes is { Length: > 0 })
                        root.Item().AlignCenter().MaxHeight(80).Image(logoBytes).FitArea();

                    // Name and portrait carry the page — this sheet is read as "who is this".
                    root.Item().PaddingTop(logoBytes is { Length: > 0 } ? 14 : 0).Row(head =>
                    {
                        head.RelativeItem().AlignMiddle().Column(c =>
                        {
                            c.Item().Text(candidate.FullName.ToUpperInvariant())
                                .FontSize(19).Bold().FontColor(AgencyBlue);
                            c.Item().PaddingTop(3).Text(V(candidate.Occupation).ToUpperInvariant())
                                .FontSize(11).FontColor(Maroon);
                            c.Item().PaddingTop(6).Text(t =>
                            {
                                t.Span("Passport  ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                t.Span(V(candidate.PassportNumber)).FontSize(9).Bold();
                                if (age is int a)
                                {
                                    t.Span("     Age  ").FontSize(8).FontColor(Colors.Grey.Darken1);
                                    t.Span($"{a}").FontSize(9).Bold();
                                }
                            });
                        });

                        head.ConstantItem(128).Height(160)
                            .Border(0.8f).BorderColor(Border).Background(Colors.Grey.Lighten4)
                            .AlignCenter().AlignMiddle()
                            .Element(e => PlaceImage(e, photoBytes, "PHOTO"));
                    });

                    root.Item().PaddingTop(14).LineHorizontal(1.2f).LineColor(Maroon);

                    ProfileBlock(root, "Personal", new (string, string)[]
                    {
                        ("Date of birth", candidate.DateOfBirth.ToString("dd MMM yyyy")),
                        ("Place of birth", V(candidate.PlaceOfBirth)),
                        ("Nationality", V(candidate.Nationality)),
                        ("Religion", V(candidate.Religion)),
                        ("Marital status", V(candidate.MaritalStatus)),
                        ("Children", candidate.NumberOfChildren?.ToString() ?? "—"),
                        ("Height", V(candidate.Height)),
                        ("Weight", V(candidate.Weight)),
                    });

                    ProfileBlock(root, "Languages & education", new (string, string)[]
                    {
                        ("English", V(candidate.EnglishLevel)),
                        ("Arabic", V(candidate.ArabicLevel)),
                        ("Education", V(candidate.Qualification)),
                    });

                    ProfileBlock(root, "Experience", new (string, string)[]
                    {
                        ("Years abroad", candidate.ExperienceAbroadYears?.ToString() ?? "—"),
                        ("Worked in", V(candidate.WorksIn)),
                        ("Cooking level", V(candidate.CookingLevel)),
                    });

                    root.Item().PaddingTop(12).Text("Skills")
                        .FontSize(10).Bold().FontColor(Maroon);
                    root.Item().PaddingTop(4).Text(
                            skills.Count > 0 ? string.Join("   ·   ", skills) : "—")
                        .FontSize(9.5f);

                    ProfileBlock(root, "Placement", new (string, string)[]
                    {
                        ("Destination", V(candidate.CountryOfTravel)),
                        ("Monthly salary", V(candidate.MonthlySalary)),
                        ("Contract period", V(candidate.ContractPeriod)),
                        ("Partner agency", V(candidate.PartnerName)),
                    });

                    if (fullPhotoBytes is { Length: > 0 })
                    {
                        root.Item().PaddingTop(14).AlignCenter()
                            .Height(190).Element(e => PlaceFullBodyImage(e, fullPhotoBytes));
                    }
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Reference ").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                    t.Span(V(candidate.ReferenceNo ?? candidate.LabourId ?? candidate.PassportNumber))
                        .FontSize(7.5f).Bold().FontColor(Colors.Grey.Darken2);
                });
            });
        });

        return document.GeneratePdf();
    }

    /// <summary>A heading and a two-column grid of label/value pairs.</summary>
    private static void ProfileBlock(
        ColumnDescriptor root, string heading, IReadOnlyList<(string Label, string Value)> rows)
    {
        root.Item().PaddingTop(12).Text(heading).FontSize(10).Bold().FontColor(Maroon);
        root.Item().PaddingTop(4).Grid(grid =>
        {
            grid.Columns(2);
            grid.HorizontalSpacing(24);
            grid.VerticalSpacing(5);
            foreach (var (label, value) in rows)
            {
                grid.Item().Row(r =>
                {
                    r.ConstantItem(96).Text(label).FontSize(8.5f).FontColor(Colors.Grey.Darken1);
                    r.RelativeItem().Text(value).FontSize(9.5f).Bold();
                });
            }
        });
    }

    private static void SectionBar(ColumnDescriptor col, string en, string ar)
    {
        col.Item().Background(Maroon).PaddingVertical(3).PaddingHorizontal(5).Row(r =>
        {
            r.RelativeItem().Text(en).FontSize(8).Bold().FontColor(Colors.White);
            r.RelativeItem().AlignRight().Text(ar).FontSize(8).Bold().FontColor(Colors.White);
        });
    }

    private static void BilingualRow(ColumnDescriptor col, string en, string ar, string value) =>
        RowPair(col, en, ar, value, 78);

    private static void CompactRow(ColumnDescriptor col, string en, string ar, string value) =>
        RowPair(col, en, ar, value, 52);

    /** Label | value only — one content column (no Arabic side column). */
    private static void SimpleRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().BorderBottom(0.4f).BorderColor(Border).Row(r =>
        {
            r.ConstantItem(90).Background(LabelBg).BorderRight(0.4f).BorderColor(Border)
                .PaddingVertical(2).PaddingHorizontal(3)
                .Text(label).FontSize(6.5f).FontColor(Colors.Grey.Darken3);

            r.RelativeItem().PaddingVertical(2).PaddingHorizontal(3)
                .AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(7.5f).Bold();
        });
    }

    private static void RowPair(ColumnDescriptor col, string en, string ar, string value, float labelWidth)
    {
        col.Item().BorderBottom(0.4f).BorderColor(Border).Row(r =>
        {
            r.ConstantItem(labelWidth).Background(LabelBg).BorderRight(0.4f).BorderColor(Border)
                .PaddingVertical(2).PaddingHorizontal(3)
                .Text(en).FontSize(6.5f).FontColor(Colors.Grey.Darken3);

            r.RelativeItem().PaddingVertical(2).PaddingHorizontal(3)
                .AlignCenter().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(7.5f).Bold();

            r.ConstantItem(labelWidth).Background(LabelBg).BorderLeft(0.4f).BorderColor(Border)
                .PaddingVertical(2).PaddingHorizontal(3)
                .AlignRight().Text(ar).FontSize(6.5f).FontColor(Colors.Grey.Darken3);
        });
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";

    private static int? AgeYears(DateOnly dob)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - dob.Year;
        if (dob > today.AddYears(-age)) age--;
        return age >= 0 ? age : null;
    }

    private static string? FormatAddress(Candidate c)
    {
        var parts = new[] { c.HouseNo, c.Woreda, c.Subcity, c.Address, c.City, c.Region, c.Country }
            .Where(s => !string.IsNullOrWhiteSpace(s));
        var joined = string.Join(", ", parts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }
}
