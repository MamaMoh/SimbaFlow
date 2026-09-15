using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Decides whose letterhead a candidate's documents are printed on.
/// </summary>
public interface IDocumentBrandingService
{
    /// <summary>
    /// The header logo for this candidate's documents: their partner agency's letterhead when
    /// they are placed with one, otherwise the agency's own.
    ///
    /// Returns null when neither has uploaded a logo, and the document falls back to the printed
    /// agency name it has always used.
    /// </summary>
    Task<byte[]?> GetHeaderLogoAsync(Candidate candidate, CancellationToken cancellationToken = default);

    /// <summary>
    /// Which CV layout this agency prints. Falls back to the default when the agency has not
    /// chosen, or chose a layout that no longer exists.
    /// </summary>
    Task<string> GetCvTemplateAsync(CancellationToken cancellationToken = default);
}
