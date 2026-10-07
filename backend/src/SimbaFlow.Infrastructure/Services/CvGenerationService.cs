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
        var agency = await _branding.GetAgencyIdentityAsync(cancellationToken);
        var document = Document.Create(container =>
            VisaFormLayout.Compose(container, candidate, photoBytes, agency));
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

        // The form names the agency presenting the applicant, which is this one for every page in
        // the batch — unlike the CV's letterhead, which follows each candidate's partner.
        var agency = await _branding.GetAgencyIdentityAsync(cancellationToken);

        var document = Document.Create(container =>
        {
            foreach (var (candidate, photo) in entries)
                VisaFormLayout.Compose(container, candidate, photo, agency);
        });
        return document.GeneratePdf();
    }
}
