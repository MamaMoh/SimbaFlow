namespace SimbaFlow.Domain.Enums;

/// <summary>
/// Types of professional/organizational identifiers for staff members.
/// </summary>
public enum IdentifierType
{
    /// <summary>Internal HR/employee number.</summary>
    InternalHR,

    /// <summary>Government-issued national ID (passport, kebele ID, etc.).</summary>
    GovernmentId,

    /// <summary>Professional association membership number.</summary>
    ProfessionalMembership,

    /// <summary>Agency license or registration number.</summary>
    AgencyLicense,

    /// <summary>Labour export license number.</summary>
    LabourExportLicense,

    /// <summary>Tax identification number.</summary>
    TaxId,

    /// <summary>Other identifier type.</summary>
    Other
}
