using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Resolves FX using platform <c>ExchangeRates</c> (public schema) — already present for multi-currency finance.
/// </summary>
public sealed class ExchangeRateService : IExchangeRateService
{
    private readonly IPlatformDbContext _platform;

    public ExchangeRateService(IPlatformDbContext platform)
    {
        _platform = platform;
    }

    public async Task<decimal> ResolveRateToEtbAsync(
        string fromCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fromCurrency))
            throw new ArgumentException("Currency is required.", nameof(fromCurrency));

        if (string.Equals(fromCurrency.Trim(), "ETB", StringComparison.OrdinalIgnoreCase))
            return 1m;

        var currency = fromCurrency.Trim().ToUpperInvariant();

        var rate = await _platform.ExchangeRates
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.FromCurrency == currency
                && r.ToCurrency == "ETB"
                && r.EffectiveDate <= asOf)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => (decimal?)r.Rate)
            .FirstOrDefaultAsync(cancellationToken);

        if (rate is null)
            throw new InvalidOperationException(
                $"No exchange rate found for {currency}→ETB on or before {asOf:yyyy-MM-dd}.");

        return rate.Value;
    }

    public async Task<decimal> ConvertToEtbAsync(
        decimal amount,
        string fromCurrency,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        var rate = await ResolveRateToEtbAsync(fromCurrency, asOf, cancellationToken);
        return Math.Round(amount * rate, 2, MidpointRounding.AwayFromZero);
    }
}
