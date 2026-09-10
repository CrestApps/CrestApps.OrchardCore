namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Identifies the lifecycle state of a scheduled callback request.
/// </summary>
public enum CallbackRequestStatus
{
    /// <summary>
    /// The callback is scheduled and waiting for its due time.
    /// </summary>
    Pending,

    /// <summary>
    /// The callback became due and was promoted into outbound work.
    /// </summary>
    Scheduled,

    /// <summary>
    /// The callback is being dialed.
    /// </summary>
    InProgress,

    /// <summary>
    /// The callback was completed.
    /// </summary>
    Completed,

    /// <summary>
    /// The callback was canceled before completion.
    /// </summary>
    Canceled,

    /// <summary>
    /// The callback failed after exhausting attempts.
    /// </summary>
    Failed,
}
