namespace SimbaFlow.Domain.Enums;

/// <summary>How a resolved dispute changed what the candidate owes.</summary>
public enum DisputeOutcome
{
    /// <summary>The candidate still owes everything; nothing is written off.</summary>
    Enforced = 0,

    /// <summary>The disputed amount (or the whole balance, if none was given) is written off.</summary>
    Waived = 1,

    /// <summary>An agreed part of the balance is written off.</summary>
    Adjusted = 2
}
