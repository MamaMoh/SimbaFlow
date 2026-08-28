using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Renders EasyEnjaz-style Application for Employment CV (bilingual table layout).
/// </summary>
public class CvGenerationService : ICvGenerationService
{
    private static readonly Color Maroon = Color.FromHex("#7A1F2B");
    private static readonly Color AgencyBlue = Color.FromHex("#1B4F9C");
    private static readonly Color Border = Color.FromHex("#222222");
    private static readonly Color LabelBg = Color.FromHex("#F5F3F1");

    static CvGenerationService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public Task<byte[]> GenerateAsync(
        Candidate candidate,
        byte[]? photoBytes = null,
        byte[]? fullPhotoBytes = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
                    root.Item().AlignCenter().Column(h =>
                    {
                        h.Item().AlignCenter().Text(agency)
                            .FontSize(11).Bold().FontColor(AgencyBlue);
                        h.Item().AlignCenter()
                            .Text("وكالة توظيف عمالة أجنبية")
                            .FontSize(9).FontColor(AgencyBlue);
                    });

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

        return Task.FromResult(document.GeneratePdf());
    }

    public Task<byte[]> GenerateVisaFormAsync(
        Candidate candidate, byte[]? photoBytes = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Modelled on the Saudi Embassy visa / enjaze form the agencies already circulate: a single
        // bilingual page with the applicant, passport, visa and sponsor blocks, a purpose-of-travel
        // line and a certification with a signature slot.
        static string D(DateOnly? d) => d?.ToString("dd/MM/yyyy") ?? "—";
        static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "—" : s;

        var agency = string.IsNullOrWhiteSpace(candidate.PartnerName)
            ? "FOREIGN EMPLOYMENT AGENCY"
            : candidate.PartnerName.ToUpperInvariant();

        var document = Document.Create(container =>
        {
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
        });

        return Task.FromResult(document.GeneratePdf());
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
