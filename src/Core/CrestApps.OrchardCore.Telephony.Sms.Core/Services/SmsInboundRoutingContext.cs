using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;

namespace CrestApps.OrchardCore.Telephony.Sms.Core.Services;

/// <summary>
/// The mutable state carried through the inbound routing chain for a single received SMS. Routers set the
/// ownership and assignment on a <b>new</b> conversation; an existing conversation keeps its assignment.
/// </summary>
public sealed class SmsInboundRoutingContext
{
    /// <summary>
    /// Gets the received message (already normalized and persisted by the provider webhook).
    /// </summary>
    public required OmnichannelMessage Message { get; init; }

    /// <summary>
    /// Gets the channel endpoint (DID) the message was received on.
    /// </summary>
    public required OmnichannelChannelEndpoint Endpoint { get; init; }

    /// <summary>
    /// Gets or sets the conversation the message belongs to (found or created before the chain runs).
    /// </summary>
    public required SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets a value indicating whether the conversation was created for this message (no prior thread existed).
    /// </summary>
    public required bool IsNewConversation { get; init; }
}
