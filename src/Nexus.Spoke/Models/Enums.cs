namespace Nexus.Spoke.Models;

public enum SpokeStatus
{
    Online,
    Offline,
    Busy
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

public enum ProjectStatus
{
    Planning,
    Active,
    Paused,
    Completed,
    Failed
}

public enum MessageDirection
{
    UserToSpoke,
    SpokeToUser,
    System
}
