namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Work that entered a queue to wait for an agent: the payload of <see cref="ContactCenterConstants.Events.QueueItemAdded"/>.
/// </summary>
public sealed class QueueItemAddedEventData
{
    /// <summary>
    /// Gets or sets the queue item that was added.
    /// </summary>
    public string QueueItemId { get; set; }

    /// <summary>
    /// Gets or sets the queue the item waits in.
    /// </summary>
    public string QueueId { get; set; }

    /// <summary>
    /// Gets or sets the activity the queued work is for.
    /// </summary>
    public string ActivityItemId { get; set; }

    /// <summary>
    /// Gets or sets the dialer profile the work was loaded under, when it is campaign inventory.
    /// </summary>
    public string DialerProfileId { get; set; }
}
