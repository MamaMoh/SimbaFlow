using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using static SimbaFlow.Infrastructure.Services.Documents.CvPrimitives;

namespace SimbaFlow.Infrastructure.Services.Documents;

/// <summary>
/// The CV forms the agencies actually circulate.
///
/// Each is a real layout an agency's partners expect, taken from the samples they sent — they
/// differ in what goes where and which facts are shown, not in styling. Numbered as the agencies
/// number them ("1st Layout"), because that is what people ask for by name.
/// </summary>
internal static class CvLayouts
{
    internal static byte[] Render(
        string template, Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo) =>
        Build(template, c, photo, fullPhoto, logo).GeneratePdf();

    /// <summary>
    /// The layout as a PNG of its first page.
    ///
    /// A preview is shown in an img tag rather than an embedded PDF: the app sends
    /// X-Frame-Options DENY on every response, which would block its own preview frame, and
    /// weakening that site-wide to show a thumbnail is the wrong trade.
    /// </summary>
    internal static byte[] RenderPreviewImage(string template, Candidate c, byte[]? logo) =>
        Build(template, c, null, null, logo)
            .GenerateImages(new ImageGenerationSettings { RasterDpi = 110 })
            .First();

    private static IDocument Build(
        string template, Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo) =>
        template switch
        {
            CvTemplates.Layout1 => Layout1(c, photo, fullPhoto, logo),
            CvTemplates.Layout2 => Layout2(c, photo, fullPhoto, logo),
            CvTemplates.Layout4 => Layout4(c, photo, fullPhoto, logo),
            CvTemplates.Layout5 => Layout5(c, photo, fullPhoto, logo),
            CvTemplates.Layout7 => Layout7(c, photo, fullPhoto, logo),
            _ => Layout3Enjaz(c, photo, fullPhoto, logo),
        };

    // ──── Shared field text ────

    private static string V(string? s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim();
    private static string YesNo(bool v) => v ? "YES" : "NO";

    private static string Age(Candidate c) => AgeYears(c.DateOfBirth)?.ToString() ?? "";
    private static string Dob(Candidate c) => c.DateOfBirth.ToString("dd/MM/yyyy");
    private static string Gender(Candidate c) =>
        c.Gender == Domain.Enums.Gender.Male ? "Male" : "Female";

    private static string Address(Candidate c) => FormatAddress(c) ?? "";

    private static string Reference(Candidate c) =>
        V(c.ReferenceNo) is { Length: > 0 } r ? r
        : V(c.LabourId) is { Length: > 0 } l ? l
        : V(c.ApplicationNo) is { Length: > 0 } a ? a
        : V(c.PassportNumber);

    /// <summary>The letterhead band every one of these forms opens with.</summary>
    private static void Letterhead(ColumnDescriptor col, byte[]? logo, Candidate c, float maxHeight = 78)
    {
        if (logo is { Length: > 0 })
        {
            col.Item().AlignCenter().MaxHeight(maxHeight).Image(logo).FitArea();
            return;
        }

        var name = string.IsNullOrWhiteSpace(c.PartnerName)
            ? "FOREIGN EMPLOYMENT AGENCY"
            : c.PartnerName.ToUpperInvariant();
        col.Item().Background(Navy).PaddingVertical(10).PaddingHorizontal(12).Column(h =>
        {
            h.Item().Text(name).FontSize(13).Bold().FontColor(Colors.White);
            h.Item().Text("وكالة توظيف عمالة أجنبية").FontSize(8.5f).FontColor(Colors.White);
        });
    }

    /// <summary>A heading bar: English left, Arabic right, reversed out of the panel colour.</summary>
    private static void Bar(ColumnDescriptor col, string en, string ar)
    {
        col.Item().Background(Navy).PaddingVertical(3.5f).PaddingHorizontal(6).Row(r =>
        {
            r.RelativeItem().Text(en).FontSize(8.5f).Bold().FontColor(Colors.White);
            r.RelativeItem().AlignRight().Text(ar).FontSize(8.5f).Bold().FontColor(Colors.White);
        });
    }

    /// <summary>Label | value | Arabic label — the row these forms are almost entirely made of.</summary>
    private static void Row3(ColumnDescriptor col, string en, string value, string ar, float label = 72)
    {
        col.Item().BorderBottom(0.4f).BorderColor(Border).Row(r =>
        {
            r.ConstantItem(label).BorderRight(0.4f).BorderColor(Border)
                .PaddingVertical(2.2f).PaddingHorizontal(3)
                .Text(en).FontSize(6.8f).FontColor(Colors.Grey.Darken3);
            r.RelativeItem().PaddingVertical(2.2f).PaddingHorizontal(3)
                .AlignCenter().Text(value).FontSize(7.8f).Bold();
            r.ConstantItem(label).BorderLeft(0.4f).BorderColor(Border)
                .PaddingVertical(2.2f).PaddingHorizontal(3)
                .AlignRight().Text(ar).FontSize(6.8f).FontColor(Colors.Grey.Darken3);
        });
    }

    /// <summary>The eight skills every one of these forms lists, in the order each form lists them.</summary>
    private static (string En, string Ar, string Value)[] Skills(Candidate c) =>
    [
        ("Washing", "الغسيل", YesNo(c.SkillWashing)),
        ("Cleaning", "التنظيف", YesNo(c.SkillCleaning)),
        ("Cooking", "الطبخ", string.IsNullOrWhiteSpace(c.CookingLevel) ? YesNo(c.SkillCooking) : c.CookingLevel),
        ("Arabic Cooking", "الطبخ العربي", YesNo(c.SkillArabicCooking)),
        ("Baby Sitting", "عناية الرضيع", YesNo(c.SkillBabysitting)),
        ("Children Care", "عناية الأطفال", YesNo(c.SkillChildCare)),
        ("Ironing", "الكوي", YesNo(c.SkillIroning)),
        ("Sewing", "خياطة", YesNo(c.SkillSewing)),
    ];

    // ════════ 1st Layout ════════
    // Photo column down the left with the name, address and passport beneath it; everything else
    // stacked on the right. The full-body shot is the point of this one — it is the form agencies
    // send when the partner wants to see the candidate before reading anything.
    private static IDocument Layout1(Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo)
    {
        var portrait = fullPhoto is { Length: > 0 } ? fullPhoto : photo;

        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

            page.Content().Row(main =>
            {
                main.ConstantItem(232).Column(left =>
                {
                    left.Item().Height(330).Border(0.8f).BorderColor(Border)
                        .Background(Colors.Grey.Lighten3)
                        .AlignCenter().AlignMiddle().Element(e => PlaceFullBodyImage(e, portrait));

                    left.Item().PaddingTop(6).Background(PanelBlue).Padding(8).Column(info =>
                    {
                        info.Item().Background(Navy).PaddingVertical(3).AlignCenter()
                            .Text("FULL NAME").FontSize(9).Bold().FontColor(Colors.White);
                        info.Item().PaddingVertical(5).AlignCenter()
                            .Text(c.FullName.ToUpperInvariant()).FontSize(10).Bold();

                        info.Item().PaddingTop(4).Background(Navy).PaddingVertical(3).AlignCenter()
                            .Text("ADDRESS").FontSize(9).Bold().FontColor(Colors.White);
                        info.Item().PaddingTop(3).AlignCenter().Text(V(Address(c))).FontSize(8.5f);
                        info.Item().AlignCenter().Text(V(c.PhoneNumber)).FontSize(8.5f);

                        info.Item().PaddingTop(6).Background(Navy).PaddingVertical(3).AlignCenter()
                            .Text("PASSPORT").FontSize(9).Bold().FontColor(Colors.White);
                        info.Item().PaddingTop(3).Column(pp =>
                        {
                            PanelRow(pp, "PASSPORT #", V(c.PassportNumber));
                            PanelRow(pp, "ISSUE", c.PassportIssueDate?.ToString("dd/MM/yyyy") ?? "");
                            PanelRow(pp, "EXPIRY", c.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "");
                            PanelRow(pp, "PLACE", V(c.PassportPlaceOfIssue));
                        });
                    });
                });

                main.ConstantItem(8);

                main.RelativeItem().Column(right =>
                {
                    Letterhead(right, logo, c, 70);

                    right.Item().PaddingTop(6);
                    Bar(right, "PERSONAL INFORMATION", "البيانات الشخصية");
                    right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                    {
                        Row3(t, "Nationality", V(c.Nationality), "الجنسية");
                        Row3(t, "Religion", V(c.Religion), "الديانة");
                        Row3(t, "Gender", Gender(c), "الجنس");
                        Row3(t, "Age", Age(c), "عمر");
                        Row3(t, "Date of Birth", Dob(c), "تاريخ الولادة");
                        Row3(t, "Marital Status", V(c.MaritalStatus), "الحالة الزوجية");
                        Row3(t, "No. of Children", c.NumberOfChildren?.ToString() ?? "", "عدد الأطفال");
                        Row3(t, "Height", V(c.Height), "ارتفاع");
                        Row3(t, "Weight", V(c.Weight), "وزن");
                    });

                    right.Item().PaddingTop(6);
                    Bar(right, "CONTRACT PERIOD", "مدة العقد");
                    right.Item().PaddingVertical(4).AlignCenter()
                        .Text(V(c.ContractPeriod)).FontSize(10).Bold();

                    right.Item().Row(sp =>
                    {
                        sp.RelativeItem().Column(s =>
                        {
                            s.Item().Background(Navy).PaddingVertical(3).AlignCenter()
                                .Text("MONTHLY SALARY").FontSize(8.5f).Bold().FontColor(Colors.White);
                            s.Item().PaddingVertical(4).AlignCenter()
                                .Text(V(c.MonthlySalary)).FontSize(9.5f).Bold();
                        });
                        sp.ConstantItem(6);
                        sp.RelativeItem().Column(s =>
                        {
                            s.Item().Background(Navy).PaddingVertical(3).AlignCenter()
                                .Text("POSITION").FontSize(8.5f).Bold().FontColor(Colors.White);
                            s.Item().PaddingVertical(4).AlignCenter()
                                .Text(V(c.Occupation)).FontSize(9.5f).Bold();
                        });
                    });

                    right.Item().PaddingTop(4).Background(Navy).PaddingVertical(5).AlignCenter()
                        .Text("خارج الدولة").FontSize(11).Bold().FontColor(Colors.White);

                    right.Item().PaddingTop(6);
                    Bar(right, "LANGUAGE & EDUCATION", "اللغة والتعليم");
                    right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                    {
                        Row3(t, "English", V(c.EnglishLevel), "الإنجليزية");
                        Row3(t, "Arabic", V(c.ArabicLevel), "العربية");
                        Row3(t, "Education", V(c.Qualification), "المستوى التعليمي");
                    });

                    right.Item().PaddingTop(6);
                    Bar(right, "Previous Employment Abroad", "خبرة خارج البلاد");
                    right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                    {
                        Row3(t, "Period", c.ExperienceAbroadYears?.ToString() ?? "", "المدة");
                        Row3(t, "Country", V(c.WorksIn), "البلد");
                    });

                    right.Item().PaddingTop(6).Border(0.5f).BorderColor(Border).Column(t =>
                    {
                        foreach (var (en, ar, val) in Skills(c)) Row3(t, en, val, ar, 66);
                        Row3(t, "Remark", V(c.Remark), "ملاحظات", 66);
                    });
                });
            });
        }));
    }

    /// <summary>A label/value line inside the 1st layout's blue side panel.</summary>
    private static void PanelRow(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingVertical(2).Row(r =>
        {
            r.ConstantItem(72).Text(label).FontSize(8).FontColor(Navy);
            r.RelativeItem().Text(value).FontSize(8.5f).Bold();
        });
    }

    // ════════ 2nd Layout ════════
    // Wide header with the headshot, a summary block, then applicant details beside a full-body
    // shot, and the skills spread across the full width in two paired columns.
    private static IDocument Layout2(Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(16);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

            page.Content().Column(root =>
            {
                Letterhead(root, logo, c, 62);

                root.Item().PaddingTop(6).Row(head =>
                {
                    head.RelativeItem().Column(box =>
                    {
                        box.Item().Background(Navy).PaddingVertical(3.5f).AlignCenter()
                            .Text("APPLICATION FOR EMPLOYMENT").FontSize(10).Bold().FontColor(Colors.White);
                        box.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Full Name", c.FullName.ToUpperInvariant(), "الاسم الكامل");
                            Row3(t, "Telephone", V(c.PhoneNumber), "رقم هاتف");
                            Row3(t, "Position", V(c.Occupation), "موضع");
                            Row3(t, "Age", Age(c), "عمر");
                            Row3(t, "Salary", V(c.MonthlySalary), "راتب شهري");
                            Row3(t, "Date of Expiry",
                                c.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "", "تاريخ الانتهاء");
                        });
                    });
                    head.ConstantItem(6);
                    head.ConstantItem(96).Height(120).Border(0.6f).BorderColor(Border)
                        .Background(Colors.Grey.Lighten4)
                        .AlignCenter().AlignMiddle().Element(e => PlaceImage(e, photo, "PHOTO"));
                });

                root.Item().PaddingTop(6).Row(body =>
                {
                    body.RelativeItem().Column(left =>
                    {
                        Bar(left, "Details of Applicant", "بيانات مقدم الطلب");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Reference No.", Reference(c), "رقم المرجع");
                            Row3(t, "Nationality", V(c.Nationality), "الجنسية");
                            Row3(t, "Passport No.", V(c.PassportNumber), "رقم جواز السفر");
                            Row3(t, "Religion", V(c.Religion), "الديانة");
                            Row3(t, "Date of Birth", Dob(c), "تاريخ الولادة");
                            Row3(t, "Place of Birth", V(c.PlaceOfBirth), "مكان الولادة");
                            Row3(t, "Complete Address", V(Address(c)), "العنوان الكامل");
                            Row3(t, "Marital Status", V(c.MaritalStatus), "الحالة الزوجية");
                            Row3(t, "No. of Children", c.NumberOfChildren?.ToString() ?? "", "عدد الأطفال");
                            Row3(t, "Height", V(c.Height), "ارتفاع");
                            Row3(t, "Weight", V(c.Weight), "وزن");
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "Languages & Education", "اللغة والتعليم");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "English", V(c.EnglishLevel), "الإنجليزية");
                            Row3(t, "Arabic", V(c.ArabicLevel), "العربية");
                            Row3(t, "Education", V(c.Qualification), "المستوى التعليمي");
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "Previous Employment Abroad", "خبرة خارج البلاد");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Period", c.ExperienceAbroadYears?.ToString() ?? "", "المدة");
                            Row3(t, "Country", V(c.WorksIn), "البلد");
                        });
                    });

                    body.ConstantItem(6);
                    body.ConstantItem(200).Border(0.6f).BorderColor(Border)
                        .Background(Colors.Grey.Lighten4)
                        .AlignCenter().AlignMiddle()
                        .Element(e => PlaceFullBodyImage(e, fullPhoto is { Length: > 0 } ? fullPhoto : photo));
                });

                root.Item().PaddingTop(6);
                Bar(root, "Skills & Experience", "خبرة العمل");
                root.Item().Border(0.5f).BorderColor(Border).Row(cols =>
                {
                    var skills = Skills(c).ToList();
                    var extra = new List<(string, string, string)>
                    {
                        ("Tutoring", "تعليم الأطفال", YesNo(c.SkillTutoring)),
                        ("Computer", "استخدام الكمبيوتر", YesNo(c.SkillComputer)),
                    };
                    var all = skills.Concat(extra).ToList();
                    var half = (all.Count + 1) / 2;

                    cols.RelativeItem().Column(t =>
                    {
                        foreach (var (en, ar, val) in all.Take(half)) Row3(t, en, val, ar, 62);
                    });
                    cols.RelativeItem().BorderLeft(0.5f).BorderColor(Border).Column(t =>
                    {
                        foreach (var (en, ar, val) in all.Skip(half)) Row3(t, en, val, ar, 62);
                    });
                });
                root.Item().Border(0.5f).BorderColor(Border).Column(t =>
                {
                    Row3(t, "Other skills", "", "خبرات أخرى");
                    Row3(t, "Remarks", V(c.Remark), "ملاحظات");
                });
            });
        }));
    }

    // ════════ 4th Layout ════════
    // Headshot sits inside the letterhead band. Work history and skills run down the left under
    // the full-body shot; passport, languages, qualification and applicant details fill the right.
    private static IDocument Layout4(Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(14);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

            page.Content().Column(root =>
            {
                root.Item().Row(band =>
                {
                    band.RelativeItem().Column(l => Letterhead(l, logo, c, 66));
                    band.ConstantItem(84).Height(66).Border(0.6f).BorderColor(Border)
                        .Background(Colors.White)
                        .AlignCenter().AlignMiddle().Element(e => PlaceImage(e, photo, "PHOTO"));
                });

                root.Item().PaddingTop(6).Row(main =>
                {
                    main.ConstantItem(248).Column(left =>
                    {
                        left.Item().Height(372).Border(0.7f).BorderColor(Border)
                            .Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Element(e => PlaceFullBodyImage(e, fullPhoto is { Length: > 0 } ? fullPhoto : photo));

                        left.Item().PaddingTop(5);
                        Bar(left, "Work Experience", "خبرة في العمل");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Period", c.ExperienceAbroadYears?.ToString() ?? "", "المدة", 60);
                            Row3(t, "Country", V(c.WorksIn), "البلد", 60);
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "Skills & Experience", "خبرة العمل");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            foreach (var (en, ar, val) in Skills(c)) Row3(t, en, val, ar, 60);
                            Row3(t, "Remarks", V(c.Remark), "ملاحظات", 60);
                        });
                    });

                    main.ConstantItem(6);

                    main.RelativeItem().Column(right =>
                    {
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Ref No.", Reference(c), "رقم");
                            Row3(t, "Job", V(c.Occupation), "الوظيفة");
                            Row3(t, "Name", c.FullName.ToUpperInvariant(), "الاسم");
                            Row3(t, "Salary", V(c.MonthlySalary), "الراتب");
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "DETAILS OF PASSPORT", "تفاصيل جواز السفر");
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Passport No.", V(c.PassportNumber), "رقم الجواز");
                            Row3(t, "Issue Date", c.PassportIssueDate?.ToString("dd/MM/yyyy") ?? "", "تاريخ الإصدار");
                            Row3(t, "Expiry Date", c.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "", "تاريخ الانتهاء");
                            Row3(t, "Place of Issue", V(c.PassportPlaceOfIssue), "مكان الإصدار");
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "LANGUAGES", "اللغات");
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "English", V(c.EnglishLevel), "الإنجليزية");
                            Row3(t, "Arabic", V(c.ArabicLevel), "العربية");
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "EDUCATIONAL QUALIFICATION", "المؤهل العلمي");
                        right.Item().Border(0.5f).BorderColor(Border)
                            .PaddingVertical(5).AlignCenter()
                            .Text(V(c.Qualification)).FontSize(9.5f).Bold();

                        right.Item().PaddingTop(5);
                        Bar(right, "DETAILS OF APPLICANT", "بيانات مقدم الطلب");
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Nationality", V(c.Nationality), "الجنسية");
                            Row3(t, "Religion", V(c.Religion), "الديانة");
                            Row3(t, "Date of Birth", Dob(c), "تاريخ الولادة");
                            Row3(t, "Place of Birth", V(c.PlaceOfBirth), "مكان الولادة");
                            Row3(t, "Leaving Town", V(c.City), "مغادرة المدينة");
                            Row3(t, "Civil Status", V(c.MaritalStatus), "الحالة الزوجية");
                            Row3(t, "No. of Children", c.NumberOfChildren?.ToString() ?? "", "عدد الأطفال");
                            Row3(t, "Height", V(c.Height), "ارتفاع");
                            Row3(t, "Weight", V(c.Weight), "الوزن");
                            Row3(t, "Complexion", V(c.Complexion), "البشرة");
                            Row3(t, "Age", Age(c), "العمر");
                            Row3(t, "Date", DateTime.UtcNow.ToString("dd/MM/yyyy"), "تاريخ");
                        });
                    });
                });
            });
        }));
    }

    // ════════ 5th Layout ════════
    // Titled "Job Application": headshot, letterhead and the placement summary share the top row;
    // personal, passport, languages and work run down the left; photo and skill ticks on the right.
    private static IDocument Layout5(Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(16);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

            page.Content().Column(root =>
            {
                root.Item().AlignCenter().PaddingBottom(5)
                    .Text("Job Application").FontSize(13).Bold();

                root.Item().Row(top =>
                {
                    top.ConstantItem(78).Height(74).Border(0.6f).BorderColor(Border)
                        .AlignCenter().AlignMiddle().Element(e => PlaceImage(e, photo, "PHOTO"));
                    top.ConstantItem(5);
                    top.RelativeItem().Column(l => Letterhead(l, logo, c, 74));
                    top.ConstantItem(5);
                    top.ConstantItem(196).Border(0.5f).BorderColor(Border).Column(t =>
                    {
                        Row3(t, "Application No", V(c.ApplicationNo), "رقم الطلب", 62);
                        Row3(t, "Post Applied For", V(c.Occupation), "الوظيفة", 62);
                        Row3(t, "Monthly Salary", V(c.MonthlySalary), "الراتب الشهري", 62);
                        Row3(t, "Contract Period", V(c.ContractPeriod), "مدة العقد", 62);
                    });
                });

                root.Item().PaddingTop(5).Border(0.5f).BorderColor(Border).Column(t =>
                    Row3(t, "Full Name", c.FullName.ToUpperInvariant(), "الاسم الكامل"));

                root.Item().PaddingTop(5).Row(body =>
                {
                    body.RelativeItem().Column(left =>
                    {
                        Bar(left, "Personal Details", "البيانات الشخصية");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Nationality", V(c.Nationality), "الجنسية");
                            Row3(t, "Religion", V(c.Religion), "الديانة");
                            Row3(t, "Date Of Birth", Dob(c), "تاريخ الميلاد");
                            Row3(t, "Place Of Birth", V(c.PlaceOfBirth), "مكان الميلاد");
                            Row3(t, "Age", Age(c), "العمر");
                            Row3(t, "Marital Status", V(c.MaritalStatus), "الحالة الاجتماعية");
                            Row3(t, "No. Of Children", c.NumberOfChildren?.ToString() ?? "", "عدد الأطفال");
                            Row3(t, "Weight", V(c.Weight), "الوزن");
                            Row3(t, "Height", V(c.Height), "الطول");
                            Row3(t, "Educational Qualification", V(c.Qualification), "المؤهل العلمي");
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "Passport Details", "بيانات جواز السفر");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Passport No.", V(c.PassportNumber), "رقم جواز السفر");
                            Row3(t, "Place of Issue", V(c.PassportPlaceOfIssue), "مكان الاصدار");
                            Row3(t, "Issue Date", c.PassportIssueDate?.ToString("dd/MM/yyyy") ?? "", "تاريخ الاصدار");
                            Row3(t, "Expiry Date", c.PassportExpiryDate?.ToString("dd/MM/yyyy") ?? "", "تاريخ الانتهاء");
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "LANGUAGES", "اللغات");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "English", V(c.EnglishLevel), "الإنجليزية");
                            Row3(t, "Arabic", V(c.ArabicLevel), "العربية");
                        });

                        left.Item().PaddingTop(5);
                        Bar(left, "Work Experience", "خبرة في العمل");
                        left.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "Period", c.ExperienceAbroadYears?.ToString() ?? "", "المدة");
                            Row3(t, "Country", V(c.WorksIn), "البلد");
                        });
                    });

                    body.ConstantItem(6);

                    body.ConstantItem(222).Column(right =>
                    {
                        right.Item().Height(300).Border(0.6f).BorderColor(Border)
                            .Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Element(e => PlaceFullBodyImage(e, fullPhoto is { Length: > 0 } ? fullPhoto : photo));

                        right.Item().PaddingTop(5).AlignRight()
                            .Text("المهارات").FontSize(8.5f).Bold();

                        // A tick box per skill, the way this form shows them.
                        right.Item().PaddingTop(3).Border(0.5f).BorderColor(Border).Row(r =>
                        {
                            foreach (var (en, ar, val) in Skills(c).Take(5))
                            {
                                r.RelativeItem().BorderRight(0.4f).BorderColor(Border)
                                    .PaddingVertical(3).Column(cell =>
                                {
                                    cell.Item().AlignCenter().Text(ar).FontSize(6);
                                    cell.Item().PaddingVertical(2).AlignCenter()
                                        .Element(e => TickBox(e, val != "NO"));
                                    cell.Item().AlignCenter().Text(en).FontSize(6);
                                });
                            }
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "Notes", "ملاحظات");
                        right.Item().Border(0.5f).BorderColor(Border).MinHeight(38)
                            .Padding(4).Text(V(c.Remark)).FontSize(8);
                    });
                });
            });
        }));
    }

    // ════════ 7th Layout ════════
    // A resume rather than a form: both photos stacked down the left, one personal-information
    // table on the right, employment history beneath it, and the skills as a tick grid across the
    // bottom — the version agencies hand to a family rather than to an office.
    private static IDocument Layout7(Candidate c, byte[]? photo, byte[]? fullPhoto, byte[]? logo)
    {
        return Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(16);
            page.DefaultTextStyle(x => x.FontFamily(DocumentFonts.Chain).FontSize(8).FontColor(Colors.Black));

            page.Content().Column(root =>
            {
                Letterhead(root, logo, c, 66);

                root.Item().PaddingTop(7).Row(body =>
                {
                    body.ConstantItem(176).Column(left =>
                    {
                        left.Item().Height(150).Border(0.8f).BorderColor(AgencyBlue)
                            .Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Element(e => PlaceImage(e, photo, "PHOTO"));
                        left.Item().PaddingVertical(3).AlignCenter()
                            .Text("الصورة كاملة").FontSize(7.5f);
                        left.Item().Height(250).Border(0.8f).BorderColor(AgencyBlue)
                            .Background(Colors.Grey.Lighten4).AlignCenter().AlignMiddle()
                            .Element(e => PlaceFullBodyImage(e, fullPhoto is { Length: > 0 } ? fullPhoto : photo));
                    });

                    body.ConstantItem(8);

                    body.RelativeItem().Column(right =>
                    {
                        right.Item().AlignCenter().Text("سيــرة ذاتية").FontSize(12).Bold().FontColor(Maroon);
                        right.Item().AlignCenter().PaddingBottom(4)
                            .Text("Candidate's Resume").FontSize(9.5f).FontColor(Maroon);

                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            t.Item().Background(PanelBlue).Row(r =>
                            {
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("DATE / التاريخ").FontSize(6.5f).Bold();
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("REFERENCE NO. / رقم المرجع").FontSize(6.5f).Bold();
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("CATEGORY / الفئة").FontSize(6.5f).Bold();
                            });
                            t.Item().BorderTop(0.4f).BorderColor(Border).Row(r =>
                            {
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text(DateTime.UtcNow.ToString("dd-MMM-yyyy")).FontSize(7.5f);
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text(Reference(c)).FontSize(7.5f).Bold();
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text(c.ExperienceAbroadYears is > 0 ? "Experienced" : "First-Time")
                                    .FontSize(7.5f);
                            });
                            Row3(t, "POSITION", V(c.Occupation), "وظيفة");
                            Row3(t, "SALARY", V(c.MonthlySalary), "راتب");
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "PERSONAL INFORMATION", "المعلومات الشخصية");
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            Row3(t, "NAME", c.FullName.ToUpperInvariant(), "الاسم");
                            Row3(t, "CITY", V(c.City), "المدينة");
                            Row3(t, "NATIONALITY", V(c.Nationality), "الجنسية");
                            Row3(t, "GENDER", Gender(c).ToUpperInvariant(), "الجنس");
                            Row3(t, "AGE", Age(c), "العمر");
                            Row3(t, "DATE OF BIRTH", Dob(c), "تاريخ الميلاد");
                            Row3(t, "EDUCATION", V(c.Qualification), "الحالة التعليمية");
                            Row3(t, "RELIGION", V(c.Religion), "الديانة");
                            Row3(t, "MARITAL STATUS", V(c.MaritalStatus), "الحالة الاجتماعية");
                            Row3(t, "NO. OF KIDS", c.NumberOfChildren?.ToString() ?? "", "عدد الأطفال");
                            Row3(t, "HEIGHT", V(c.Height), "الطول");
                            Row3(t, "WEIGHT", V(c.Weight), "الوزن");
                            Row3(t, "ARABIC LANGUAGE", V(c.ArabicLevel), "اللغة العربية");
                            Row3(t, "ENGLISH LANGUAGE", V(c.EnglishLevel), "اللغة الإنجليزية");
                        });

                        right.Item().PaddingTop(5);
                        Bar(right, "EMPLOYMENT BACKGROUND", "خلفية عن العمل");
                        right.Item().Border(0.5f).BorderColor(Border).Column(t =>
                        {
                            t.Item().Background(PanelBlue).Row(r =>
                            {
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("COUNTRY / الدولة").FontSize(6.5f).Bold();
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("POSITION / وظيفة").FontSize(6.5f).Bold();
                                r.RelativeItem().PaddingVertical(2.5f).AlignCenter()
                                    .Text("DURATION / مدة العمل").FontSize(6.5f).Bold();
                            });
                            t.Item().BorderTop(0.4f).BorderColor(Border).Row(r =>
                            {
                                r.RelativeItem().PaddingVertical(3).AlignCenter()
                                    .Text(V(c.WorksIn)).FontSize(7.5f);
                                r.RelativeItem().PaddingVertical(3).AlignCenter()
                                    .Text(V(c.Occupation)).FontSize(7.5f);
                                r.RelativeItem().PaddingVertical(3).AlignCenter()
                                    .Text(c.ExperienceAbroadYears?.ToString() ?? "").FontSize(7.5f);
                            });
                        });
                    });
                });

                // Skills as a tick grid across the bottom — four to a row, English and Arabic.
                root.Item().PaddingTop(8).Grid(grid =>
                {
                    grid.Columns(4);
                    grid.HorizontalSpacing(4);
                    grid.VerticalSpacing(4);
                    foreach (var (en, ar, val) in Skills(c))
                    {
                        grid.Item().Border(0.5f).BorderColor(Border)
                            .Background(Colors.Grey.Lighten4).PaddingVertical(5).Column(cell =>
                        {
                            cell.Item().AlignCenter().Text($"{en}  {ar}").FontSize(7);
                            cell.Item().AlignCenter().PaddingTop(3)
                                .Element(e => TickBox(e, val != "NO"));
                            cell.Item().AlignCenter().PaddingTop(2)
                                .Text(val).FontSize(6.5f).Bold()
                                .FontColor(val == "NO" ? Colors.Grey.Darken1 : Navy);
                        });
                    }
                });
            });
        }));
    }

    // ════════ 3rd Layout ════════
    private static IDocument Layout3Enjaz(
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

        return document;
    }


}