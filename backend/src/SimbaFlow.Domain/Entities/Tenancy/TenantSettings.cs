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

    /// <summary>
    /// What a new candidate form starts with. Agencies deploy to one corridor and one job for
    /// months at a time, so typing the same three answers into every registration is wasted
    /// keystrokes. These only pre-fill a blank form — they never overwrite a saved record.
    /// </summary>
    public IntakeDefaults Intake { get; set; } = new();
}

public class IntakeDefaults
{
    /// <summary>"0" male, "1" female — matching the Gender enum the form posts.</summary>
    public string Gender { get; set; } = "1";
    public string Occupation { get; set; } = "House Maid";
    public string CountryOfTravel { get; set; } = "";
    public string ContractPeriod { get; set; } = "2 Years";
}
