namespace CrestApps.OrchardCore.ContactCenter.Core.Models.Dispatch;

/// <summary>
/// Counts the records of a queue that share one <see cref="DispatchBlocker"/>.
/// </summary>
public sealed class DispatchBucket
{
    /// <summary>
    /// Gets or sets the reason the records are held back, or <see cref="DispatchBlocker.DialableNow"/> when they are not.
    /// </summary>
    public DispatchBlocker Blocker { get; set; }

    /// <summary>
    /// Gets or sets the number of records in the bucket.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets the earliest time a record of the bucket becomes due, when the reason is a wait that ends by itself
    /// (scheduled for later, or a retry cool-down). It is <see langword="null"/> for any other reason.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }
}
