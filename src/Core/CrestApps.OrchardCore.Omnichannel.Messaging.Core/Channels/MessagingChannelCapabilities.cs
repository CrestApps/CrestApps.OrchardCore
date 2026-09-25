namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Describes what a messaging channel can carry and which workspace rules apply to it.
/// </summary>
public sealed class MessagingChannelCapabilities
{
    /// <summary>
    /// Gets a value indicating whether a message carries a subject line, as an email does.
    /// </summary>
    public bool SupportsSubject { get; init; }

    /// <summary>
    /// Gets a value indicating whether a message can carry media attachments.
    /// </summary>
    public bool SupportsMedia { get; init; }

    /// <summary>
    /// Gets a value indicating whether the channel's providers report delivery receipts, so the workspace shows
    /// a delivery state on each outbound message.
    /// </summary>
    public bool SupportsDeliveryReceipts { get; init; }

    /// <summary>
    /// Gets a value indicating whether a group message may be sent on the channel as individual one-to-one
    /// threads.
    /// </summary>
    public bool SupportsBroadcast { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether messages on the channel are subject to quiet hours in the contact's local
    /// time, as a text message is.
    /// </summary>
    public bool ObservesQuietHours { get; init; }

    /// <summary>
    /// Gets the longest body the channel accepts, or <see langword="null"/> when there is no practical limit.
    /// </summary>
    public int? MaxBodyLength { get; init; }
}
