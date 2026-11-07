namespace CrestApps.OrchardCore.WorkQueues;

/// <summary>
/// A durable unit of work that one worker at a time owns through a lease: an outbox message, an inbox delivery, an
/// outreach batch. <see cref="ILeasedWorkQueue{T}"/> keeps these fields; the work item only stores them.
/// </summary>
/// <remarks>
/// <see cref="NextAttemptUtc"/> is when the item can next be claimed: the retry time while it is pending, and the
/// lease expiry while it is claimed. A worker that stops without settling its claim therefore loses it once the lease
/// runs out, and the item is claimed again.
/// </remarks>
public interface ILeasedWorkItem
{
    /// <summary>
    /// Gets or sets the stable identifier of the item.
    /// </summary>
    string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the state of the item.
    /// </summary>
    LeasedWorkItemStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the opaque token of the worker that owns the current claim, or <see langword="null"/> when nobody
    /// owns the item.
    /// </summary>
    string OwnerToken { get; set; }

    /// <summary>
    /// Gets or sets the fence token, which grows with every claim. A worker settles its claim only while the stored
    /// fence token is still the one it claimed with.
    /// </summary>
    long FenceToken { get; set; }

    /// <summary>
    /// Gets or sets the number of failed attempts.
    /// </summary>
    int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the item can next be claimed: its retry time while pending, its lease expiry while
    /// claimed.
    /// </summary>
    DateTime NextAttemptUtc { get; set; }

    /// <summary>
    /// Gets or sets the error of the last failed attempt.
    /// </summary>
    string LastError { get; set; }
}
