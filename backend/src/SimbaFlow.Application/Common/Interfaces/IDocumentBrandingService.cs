using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>
/// Who an agency says it is on a printed document: the name a reader sees and how to reach them.
/// </summary>
/// <param name="Name">Already upper-cased — the embassy form prints it that way.</param>
public sealed record AgencyIdentity(string Name, string? Email, string? Phone, string? LicenseNumber);

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
    /// The agency's own name and contact details.
    ///
    /// The agency's own, not the partner's, even where the letterhead is the partner's. On the
    /// embassy form this names the party presenting the applicant, which is the licensed Ethiopian
    /// agency — the Saudi recruiter is the sponsor's representative and appears elsewhere.
    /// </summary>
    Task<AgencyIdentity> GetAgencyIdentityAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Which CV layout this agency prints. Falls back to the default when the agency has not
    /// chosen, or chose a layout that no longer exists.
    /// </summary>
    Task<string> GetCvTemplateAsync(CancellationToken cancellationToken = default);
}
