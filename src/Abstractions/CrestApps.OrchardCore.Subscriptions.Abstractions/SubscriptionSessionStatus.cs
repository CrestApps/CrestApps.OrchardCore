namespace CrestApps.OrchardCore.Subscriptions;

/// <summary>
/// Describes the lifecycle state of a <see cref="SubscriptionSession"/>.
/// </summary>
public enum SubscriptionSessionStatus
{
    /// <summary>
    /// The session has been created but not yet completed.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// The session was canceled before completion.
    /// </summary>
    Canceled = 1,

    /// <summary>
    /// The session was temporarily suspended.
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// The session completed successfully.
    /// </summary>
    Completed = 3,
}
