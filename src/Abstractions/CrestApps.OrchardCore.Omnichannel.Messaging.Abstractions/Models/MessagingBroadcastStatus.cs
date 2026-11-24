namespace CrestApps.OrchardCore.Omnichannel.Messaging.Models;

/// <summary>
/// The lifecycle state of a broadcast (a one-composer, many-recipient send in which each recipient gets the message on
/// their own, with no cross-recipient visibility; a conversation opens when a recipient replies).
/// </summary>
public enum MessagingBroadcastStatus
{
    /// <summary>
    /// The broadcast is being composed and has not been queued.
    /// </summary>
    Draft,

    /// <summary>
    /// The broadcast has been queued and is awaiting the background fan-out.
    /// </summary>
    Queued,

    /// <summary>
    /// The background fan-out is in progress.
    /// </summary>
    Running,

    /// <summary>
    /// Every recipient has been processed. Messages the channel held back may still be queued; such a broadcast is
    /// shown as still sending until they are sent.
    /// </summary>
    Completed,

    /// <summary>
    /// The broadcast failed before completing (for example, an unresolvable sending number).
    /// </summary>
    Failed,
}
