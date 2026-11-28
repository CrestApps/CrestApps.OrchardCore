using OrchardCore.Data.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// What became of the broadcast messages the channel held back: how many of each broadcast's the outbox later sent, and
/// how many it gave up on. Only the outbox writes it, and the broadcast run only writes the broadcast, so neither can
/// overwrite a count the other just saved; a broadcast's progress is read from both.
/// </summary>
public sealed class MessagingBroadcastQueueOutcomes : Document
{
    /// <summary>
    /// Gets or sets the outcomes by broadcast identifier.
    /// </summary>
    public Dictionary<string, MessagingBroadcastQueueOutcome> Broadcasts { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>
/// What the outbox did with one broadcast's held-back messages.
/// </summary>
public sealed class MessagingBroadcastQueueOutcome
{
    /// <summary>
    /// Gets or sets how many of the held-back messages the outbox sent.
    /// </summary>
    public int Sent { get; set; }

    /// <summary>
    /// Gets or sets how many of the held-back messages the outbox gave up on.
    /// </summary>
    public int Failed { get; set; }
}
