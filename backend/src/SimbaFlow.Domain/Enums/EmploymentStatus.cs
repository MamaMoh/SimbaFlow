namespace SimbaFlow.Domain.Enums;

/// <summary>
/// Employment lifecycle states for agency staff.
/// </summary>
public enum EmploymentStatus
{
    /// <summary>Currently employed and active in the system.</summary>
    Active,

    /// <summary>Employment pending (HR drafted, IT not yet provisioned).</summary>
    Pending,

    /// <summary>On approved leave.</summary>
    OnLeave,

    /// <summary>Suspended pending investigation. Login blocked.</summary>
    Suspended,

    /// <summary>Employment ended. Triggers token revocation and task reassignment.</summary>
    Terminated,

    /// <summary>Resigned voluntarily.</summary>
    Resigned
}
