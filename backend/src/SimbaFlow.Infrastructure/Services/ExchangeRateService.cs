using Microsoft.EntityFrameworkCore;
using SimbaFlow.Application.Common.Interfaces;

namespace SimbaFlow.Infrastructure.Services;

/// <summary>
/// Resolves FX using platform <c>ExchangeRates</c> (public schema).
///
/// The signed-in agency's own rate is used when it has one for the pair on or before the date;
/// otherwise the platform default. One agency's rates never reach another agency's conversions.
/// </summary>
public sealed class ExchangeRateService : IExchangeRateService
{
    private readonly IPlatformDbContext _platform;
    private readonly ICurrentUserService _currentUser;

    public ExchangeRateService(IPlatformDbContext platform, ICurrentUserService currentUser)
    {
        _platform = platform;
        _currentUser = currentUser;
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
        var tenantId = _currentUser.TenantId;

        var candidates = _platform.ExchangeRates
            .AsNoTracking()
            .Where(r => !r.IsDeleted
                && r.FromCurrency == currency
                && r.ToCurrency == "ETB"
                && r.EffectiveDate <= asOf);

        decimal? rate = null;
        if (tenantId is Guid tid)
        {
            rate = await candidates
                .Where(r => r.TenantId == tid)
                .OrderByDescending(r => r.EffectiveDate)
                .Select(r => (decimal?)r.Rate)
                .FirstOrDefaultAsync(cancellationToken);
        }

        rate ??= await candidates
            .Where(r => r.TenantId == null)
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
