using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Generates candidate PDF documents (Enjaz-style CV, visa form).
/// </summary>
public interface ICvGenerationService
{
    /// <summary>
    /// Render an EasyEnjaz-style Application for Employment CV.
    /// </summary>
    Task<byte[]> GenerateAsync(
        Candidate candidate,
        byte[]? photoBytes = null,
        byte[]? fullPhotoBytes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Render an Enjaz / visa application style PDF (photo + sponsor/visa block).
    /// </summary>
    Task<byte[]> GenerateVisaFormAsync(
        Candidate candidate,
        byte[]? photoBytes = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// One document holding an enjaze page per candidate.
    ///
    /// A zip of separate files is fine for filing but useless at a printer, and the embassy run is
    /// printed as a batch.
    /// </summary>
    /// <summary>
    /// Render a named layout with stand-in details, so someone choosing a CV form can see it
    /// before committing every candidate's paperwork to it.
    ///
    /// Uses the agency's own letterhead rather than a placeholder — half of what you are judging
    /// is how your own branding sits on the page.
    /// </summary>
    Task<byte[]> GeneratePreviewAsync(string template, CancellationToken cancellationToken = default);

    /// <summary>The same preview as a PNG of the first page, for showing on screen.</summary>
    Task<byte[]> GeneratePreviewImageAsync(string template, CancellationToken cancellationToken = default);

    Task<byte[]> GenerateVisaFormsAsync(
        IReadOnlyList<(Candidate Candidate, byte[]? Photo)> entries,
        CancellationToken cancellationToken = default);
}
