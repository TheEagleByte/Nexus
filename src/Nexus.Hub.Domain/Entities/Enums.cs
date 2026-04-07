namespace Nexus.Hub.Domain.Entities;

public enum SpokeStatus
{
    Online,
    Offline,
    Busy
}

public enum ProjectStatus
{
    Planning,
    Active,
    Paused,
    Completed,
    Failed,
    Archived
}

public enum JobStatus
{
    Queued,
    AwaitingApproval,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum JobType
{
    Implement,
    Test,
    Refactor,
    Investigate,
    Custom
}

public enum MessageDirection
{
    UserToSpoke,
    SpokeToUser,
    System
}

public enum ConversationRole
{
    User,
    Assistant
}

public enum PendingActionType
{
    PlanReview,
    PreExecution,
    PostExecution,
    PrReview
}

public enum PendingActionStatus
{
    Pending,
    Approved,
    Rejected,
    Resolved
}
