namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// Value object stored as JSONB in TenantInfo. Per-agency configuration.
/// </summary>
public class TenantSettings
{
    public string DefaultLanguage { get; set; } = "en";
    public string[] SupportedLanguages { get; set; } = ["en", "am"];
    public string DefaultCurrency { get; set; } = "ETB";
    public string[] SupportedCurrencies { get; set; } = ["ETB", "USD", "SAR", "AED"];
    public int MaxFileUploadSizeMB { get; set; } = 10;
    public bool SignalREnabled { get; set; } = true;
    public bool BotEnabled { get; set; }
}
