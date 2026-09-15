using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SimbaFlow.Application.Common.Interfaces;
using SimbaFlow.Domain.Entities.Candidates;
using SimbaFlow.Domain.Services;
using SimbaFlow.Infrastructure.Services.Documents;
using static SimbaFlow.Infrastructure.Services.Documents.CvPrimitives;

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

        return CvLayouts.Render(template, candidate, photoBytes, fullPhotoBytes, logoBytes);
    }

    public async Task<byte[]> GeneratePreviewAsync(
        string template, CancellationToken cancellationToken = default)
    {
        var sample = CvPreviewSample.Candidate();
        // The sample has no partner agency, so this resolves to the agency's own letterhead.
        var logoBytes = await _branding.GetHeaderLogoAsync(sample, cancellationToken);
        return CvLayouts.Render(CvTemplates.Normalise(template), sample, null, null, logoBytes);
    }

    public async Task<byte[]> GeneratePreviewImageAsync(
        string template, CancellationToken cancellationToken = default)
    {
        var sample = CvPreviewSample.Candidate();
        var logoBytes = await _branding.GetHeaderLogoAsync(sample, cancellationToken);
        return CvLayouts.RenderPreviewImage(CvTemplates.Normalise(template), sample, logoBytes);
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
}
