namespace SimbaFlow.Domain.Enums;

/// <summary>Where a subscription invoice has got to.</summary>
public enum InvoiceStatus
{
    /// <summary>Generated but not yet sent to the agency.</summary>
    Draft = 0,

    /// <summary>Sent. The agency owes this.</summary>
    Issued = 1,

    Paid = 2,

    /// <summary>Cancelled — raised in error, or superseded. Never counts as owed.</summary>
    Void = 3,
}
