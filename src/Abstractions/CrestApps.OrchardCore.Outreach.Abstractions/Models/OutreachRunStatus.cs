namespace CrestApps.OrchardCore.Outreach.Models;

/// <summary>
/// Where an outreach run's sending is.
/// </summary>
public enum OutreachRunStatus
{
    /// <summary>
    /// Started; its first batch has not been sent yet.
    /// </summary>
    Queued,

    /// <summary>
    /// Sending, one batch at a time.
    /// </summary>
    Running,

    /// <summary>
    /// Every recipient has been handled.
    /// </summary>
    Completed,

    /// <summary>
    /// Stopped before the end; the rest is not sent.
    /// </summary>
    Canceled,

    /// <summary>
    /// Gave up, for example because a batch kept failing.
    /// </summary>
    Failed,
}
