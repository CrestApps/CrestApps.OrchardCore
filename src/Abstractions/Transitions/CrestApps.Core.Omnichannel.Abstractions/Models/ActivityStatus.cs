namespace CrestApps.Core.Omnichannel.Models;

/// <summary>
/// Specifies the activity status options.
/// </summary>
public enum ActivityStatus
{
    NotStated,
    AwaitingAgentResponse,
    AwaitingCustomerAnswer,
    Completed,
    Pending,
    Scheduled,
    Reserved,
    Dialing,
    InProgress,
    Failed,
    Cancelled,
    Purged,
}
