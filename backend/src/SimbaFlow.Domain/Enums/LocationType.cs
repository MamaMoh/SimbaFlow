namespace SimbaFlow.Domain.Enums;

public enum LocationType
{
    /// <summary>Top-level agency headquarters.</summary>
    Headquarters,

    /// <summary>Branch office within the country.</summary>
    BranchOffice,

    /// <summary>Overseas partner office.</summary>
    OverseasOffice,

    /// <summary>Embassy or consulate processing location.</summary>
    Embassy,

    /// <summary>Medical examination center.</summary>
    MedicalCenter,

    /// <summary>Government labour office (LMIS).</summary>
    LabourOffice,

    /// <summary>Airport or departure point.</summary>
    DeparturePoint,

    /// <summary>Destination arrival point.</summary>
    ArrivalPoint
}
