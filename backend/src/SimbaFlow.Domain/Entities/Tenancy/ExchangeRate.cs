using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Currency exchange rates stored in the public schema.
/// Used for multi-currency commission and payment calculations.
///
/// A row with no <see cref="TenantId"/> is a platform default, kept by the platform team. A row
/// with one belongs to that agency alone: the rates an agency types change only its own
/// conversions, and its own rate wins over the default when both exist.
/// </summary>
public class ExchangeRate : BaseEntity
{
    /// <summary>The agency this rate belongs to; null for a platform default.</summary>
    public Guid? TenantId { get; set; }

    public string FromCurrency { get; set; } = string.Empty; // ISO 4217 (3 chars)
    public string ToCurrency { get; set; } = string.Empty;   // ISO 4217 (3 chars)
    public decimal Rate { get; set; }
    public DateOnly EffectiveDate { get; set; }
    public string? Source { get; set; } // "manual" or "api"
}
