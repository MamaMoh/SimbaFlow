using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Currency exchange rates stored in the public schema.
/// Used for multi-currency commission and payment calculations.
/// </summary>
public class ExchangeRate : BaseEntity
{
    public string FromCurrency { get; set; } = string.Empty; // ISO 4217 (3 chars)
    public string ToCurrency { get; set; } = string.Empty;   // ISO 4217 (3 chars)
    public decimal Rate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Source { get; set; } // "manual" or "api"
}
