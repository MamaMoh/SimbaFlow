namespace SimbaFlow.Domain.Enums;

public enum CandidateStatus
{
    Active = 0,
    Archived = 1,
    /// <summary>Withdrawn from the active pipeline (e.g. medically unfit). Appended — existing rows keep their value.</summary>
    Inactive = 2
}
