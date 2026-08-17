namespace SimbaFlow.Application.Common.Interfaces;

public interface IExchangeRateService
{
    /// <summary>
    /// Resolves rate from <paramref name="fromCurrency"/> to ETB as of <paramref name="asOf"/>.
    /// Returns 1 when currency is ETB. Throws if non-ETB rate is missing.
    /// </summary>
    Task<decimal> ResolveRateToEtbAsync(
        string fromCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default);

    Task<decimal> ConvertToEtbAsync(
        decimal amount,
        string fromCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default);
}
