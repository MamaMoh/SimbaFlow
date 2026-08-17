namespace SimbaFlow.Domain.Enums;

/// <summary>
/// Shell status for Unit 4. Unit 5 extends Partial / Settled / Disputed on finance flows.
/// </summary>
public enum CommissionStatus
{
    Open = 0,
    Partial = 1,
    Settled = 2,
    Disputed = 3
}
