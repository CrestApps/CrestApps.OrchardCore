namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// How a leased work queue stores and retires its items.
/// </summary>
public class LeasedWorkQueueOptions
{
    /// <summary>
    /// Gets or sets the YesSql collection the items live in, or <see langword="null"/> for the default collection.
    /// </summary>
    public string CollectionName { get; set; }

    /// <summary>
    /// Gets or sets how many failed attempts an item gets before it is dead-lettered. The default is 10.
    /// </summary>
    public int MaxAttempts { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether a completed item is deleted. When <see langword="false"/> (the default) it is kept as
    /// completed, for example as an idempotency tombstone that a retention policy removes later.
    /// </summary>
    public bool RemoveCompletedItems { get; set; }
}
