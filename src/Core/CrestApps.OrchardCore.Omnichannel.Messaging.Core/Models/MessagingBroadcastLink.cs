namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Kept on an outbound message sent for a broadcast, so a message the channel held back (its sending address was at its
/// limit, or the provider was briefly unreachable) is counted on the broadcast when the outbox sends it, or gives up on it.
/// </summary>
public sealed class MessagingBroadcastLink
{
    /// <summary>
    /// Gets or sets the identifier of the broadcast the message was sent for.
    /// </summary>
    public string BroadcastId { get; set; }
}
