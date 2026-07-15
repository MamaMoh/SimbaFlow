namespace SimbaFlow.Domain.Enums;

public enum WorkflowEventType
{
    Registered = 0,
    StageTransitioned = 1,
    StatusUpdated = 2,
    FieldUpdated = 3,
    ActionExecuted = 4,
    MirrorViewActivated = 5,
    MirrorViewDeactivated = 6,
    ExceptionFlagged = 7,
    Archived = 8
}
