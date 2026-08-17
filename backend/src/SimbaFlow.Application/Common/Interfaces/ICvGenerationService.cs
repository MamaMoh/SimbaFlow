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
}
