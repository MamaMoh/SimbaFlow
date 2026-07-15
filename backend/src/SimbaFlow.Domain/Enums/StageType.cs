namespace SimbaFlow.Domain.Enums;

public enum StageType
{
    /// <summary>Single status track with simple transitions.</summary>
    Simple = 0,

    /// <summary>Multiple independent tracks running simultaneously (e.g., Medical + Tasheer).</summary>
    ParallelTrack = 1,

    /// <summary>Sequential milestones (e.g., Uploaded → Verified → Issued).</summary>
    MilestoneSequence = 2
}
