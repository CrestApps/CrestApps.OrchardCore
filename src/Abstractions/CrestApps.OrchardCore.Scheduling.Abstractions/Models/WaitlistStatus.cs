namespace CrestApps.OrchardCore.Scheduling.Models;

/// <summary>
/// The state of a waitlist entry.
/// </summary>
public enum WaitlistStatus
{
    /// <summary>
    /// The customer is waiting for a time to free up.
    /// </summary>
    Waiting,

    /// <summary>
    /// A time freed up and the customer was told.
    /// </summary>
    Notified,

    /// <summary>
    /// The customer booked.
    /// </summary>
    Booked,

    /// <summary>
    /// The entry was removed.
    /// </summary>
    Removed,
}
