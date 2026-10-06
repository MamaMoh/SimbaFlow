using SimbaFlow.Domain.Common;

namespace SimbaFlow.Domain.Entities.Tenancy;

/// <summary>
/// What a blank candidate form starts with for this agency.
///
/// One row per tenant schema (the database is already per-agency, so there is no TenantId).
/// Replaces the Intake + Documents.CvTemplate JSON that used to live on platform TenantInfo.
/// </summary>
public class AgencyIntakeDefaults : BaseEntity
{
    /// <summary>Always 1. Unique so the table cannot grow a second row.</summary>
    public int SingletonKey { get; set; } = 1;

    /// <summary>"0" male, "1" female, or empty for no default.</summary>
    public string Gender { get; set; } = "1";
    public string Occupation { get; set; } = "HOUSE MAID";
    public string Religion { get; set; } = "";
    public string Nationality { get; set; } = "Ethiopia";
    public string PassportType { get; set; } = "Normal";
    public string MaritalStatus { get; set; } = "Single";
    public string CountryOfTravel { get; set; } = "";
    public string ContractPeriod { get; set; } = "2 Years";
    public string CvTemplate { get; set; } = "layout3";
    public string CookingLevel { get; set; } = "";
}
