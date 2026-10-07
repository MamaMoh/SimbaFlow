using SimbaFlow.Domain.Services;

namespace SimbaFlow.Application.Common.Interfaces;

/// <summary>Reads the placement details out of an uploaded Saudi employment contract.</summary>
public interface IContractReaderService
{
    /// <summary>
    /// Returns what the file says, or an empty result if it is not one of these contracts.
    /// Never throws for a bad file — an unreadable upload is an answer, not a failure.
    /// </summary>
    SaudiContractDetails Read(Stream pdf);
}
