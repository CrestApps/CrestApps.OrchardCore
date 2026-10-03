using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// What an inbound entry point that answers a messaging channel has beyond the target and opening hours every entry
/// point has: how a queue's conversations are handed out, and the automatic replies. Stored in the entry point's
/// properties by the messaging workspace.
/// </summary>
public sealed class MessagingEntryPointSettings
{
    /// <summary>
    /// Gets or sets how conversations for a queue target are handed out. Ignored for an agent target.
    /// </summary>
    public ConversationDistributionMode DistributionMode { get; set; } = ConversationDistributionMode.SharedPool;

    /// <summary>
    /// Gets or sets the reply sent to a contact who writes in, at most once a day per conversation.
    /// </summary>
    public string AutoReplyMessage { get; set; }

    /// <summary>
    /// Gets or sets the reply sent instead while the entry point is closed. When empty, the ordinary auto-reply is sent
    /// whatever the time.
    /// </summary>
    public string ClosedAutoReplyMessage { get; set; }
}
