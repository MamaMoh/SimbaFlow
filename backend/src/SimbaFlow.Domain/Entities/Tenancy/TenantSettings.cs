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

    /// <summary>How this agency's generated paperwork is laid out.</summary>
    public DocumentSettings Documents { get; set; } = new();
}

public class DocumentSettings
{
    /// <summary>
    /// Which CV layout this agency prints by default.
    ///
    /// Agencies circulate different forms — the one a partner will accept is a matter of who you
    /// are sending it to, not of taste — so the layout is a per-agency setting rather than
    /// something chosen afresh on every download. See CvTemplates for the known values.
    /// </summary>
    public string CvTemplate { get; set; } = "layout3";
}

public class IntakeDefaults
{
    /// <summary>
    /// "0" male, "1" female, or empty for no default.
    ///
    /// Empty matters: most agencies in this corridor deploy women, so female is a useful starting
    /// point — but an agency that places both should be able to leave it unanswered rather than
    /// having the form guess for them on every registration.
    /// </summary>
    public string Gender { get; set; } = "1";
    public string Occupation { get; set; } = "HOUSE MAID";
    public string Religion { get; set; } = "";
    public string Nationality { get; set; } = "Ethiopia";
    public string PassportType { get; set; } = "Normal";
    public string MaritalStatus { get; set; } = "Single";
    public string CountryOfTravel { get; set; } = "";
    public string ContractPeriod { get; set; } = "2 Years";
}
