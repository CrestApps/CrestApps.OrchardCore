using YesSql.Indexes;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;

/// <summary>
/// The outbound messages waiting for another attempt: queued after a failed send, or held back until their sending
/// address may send again. Only those messages are in it, so the outbox finds what is due however many messages the
/// tenant has ever sent.
/// </summary>
public sealed class MessagingOutboxIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the channel the message is sent on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the address the message is sent from.
    /// </summary>
    public string ServiceAddress { get; set; }

    /// <summary>
    /// Gets or sets when the message is next tried.
    /// </summary>
    public DateTime NextAttemptUtc { get; set; }
}
