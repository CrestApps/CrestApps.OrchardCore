using CrestApps.Core;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// A one-composer, many-recipient send on one messaging channel. Each recipient gets the message on their own (no
/// cross-visibility), the compliant default. It opens no conversation: it joins one a recipient already has, and a
/// conversation opens when a recipient replies. A durable background task works the recipient list so a large send
/// survives a restart and never double-sends a processed recipient.
/// </summary>
public sealed class MessagingBroadcast : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the broadcast name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the channel the broadcast is sent on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets our address (an endpoint of the channel) every message goes out from.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets the subject line sent to every recipient, on a channel that supports one.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the message body sent to every recipient.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the recipient addresses on the channel.
    /// </summary>
    public IList<string> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the recipients processed before broadcasts were sent through the outreach engine. They are still
    /// read, so a broadcast that was half sent then is never sent to them again; the engine keeps its own progress.
    /// </summary>
    public IList<string> ProcessedRecipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the identifier of the agent who owns the broadcast and sends its messages.
    /// </summary>
    public string OwnerAgentId { get; set; }

    /// <summary>
    /// Gets or sets the broadcast lifecycle status.
    /// </summary>
    public MessagingBroadcastStatus Status { get; set; } = MessagingBroadcastStatus.Draft;

    /// <summary>
    /// Gets or sets the number of recipients the outreach run counted as sent, which includes the messages the channel
    /// held back. <see cref="MessagingBroadcastExtensions.GetSentCount(MessagingBroadcast, MessagingBroadcastQueueOutcome)"/>
    /// leaves those out.
    /// </summary>
    public int SentCount { get; set; }

    /// <summary>
    /// Gets or sets the number of recipients the outreach run could not send to.
    /// </summary>
    public int FailedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of messages the channel held back (the sending address was at its limit, or the provider
    /// was briefly unreachable) for the outbox to send later. Only the broadcast's run writes it; what the outbox did
    /// with them is in <see cref="MessagingBroadcastQueueOutcomes"/>, and
    /// <see cref="MessagingBroadcastExtensions.GetQueuedCount(MessagingBroadcast, MessagingBroadcastQueueOutcome)"/>
    /// gives how many still wait.
    /// </summary>
    public int QueuedCount { get; set; }

    /// <summary>
    /// Gets or sets the number of recipients that were not sent to because they opted out of the channel.
    /// </summary>
    public int SkippedCount { get; set; }

    /// <summary>
    /// Gets or sets the outreach run that sends the broadcast, once it has started.
    /// </summary>
    public string OutreachRunId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast finished processing.
    /// </summary>
    public DateTime? CompletedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the broadcast was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
