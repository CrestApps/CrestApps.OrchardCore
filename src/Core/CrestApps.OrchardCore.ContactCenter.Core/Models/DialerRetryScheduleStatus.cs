namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of a request to schedule the next attempt of a dialer campaign record.
/// </summary>
public enum DialerRetryScheduleStatus
{
    /// <summary>
    /// The next attempt was created and queued.
    /// </summary>
    Scheduled,

    /// <summary>
    /// No attempt is left under the dialer profile's limit.
    /// </summary>
    AttemptsExhausted,

    /// <summary>
    /// The activity could not be retried: it does not exist, is not dialer campaign work, or has not finished.
    /// </summary>
    NotRetryable,
}
