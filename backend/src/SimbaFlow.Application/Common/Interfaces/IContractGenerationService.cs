using SimbaFlow.Domain.Entities.Candidates;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>Party details that vary per agency, filled from the tenant and the linked partner.</summary>
public sealed record ContractParties(
    string SaudiAgencyName,
    string? SaudiLicenseNo,
    string? SaudiPhone,
    string? SaudiAddress,
    string? SaudiCity,
    string? SaudiEmail,
    string EthiopianAgencyName,
    string? EthiopianLicenseNo,
    string? EthiopianAddress,
    string? EthiopianCity,
    string? EthiopianPhone,
    string? EthiopianEmail);

public interface IContractGenerationService
{
    /// <summary>
    /// The MoLS Standard Employment Contract for a candidate bound for Saudi Arabia, bilingual
    /// English/Arabic, ready to print and sign.
    /// </summary>
    Task<byte[]> GenerateAsync(
        Candidate candidate,
        ContractParties parties,
        CancellationToken ct = default);
}
