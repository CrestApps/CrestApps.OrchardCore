using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The mutable state carried through the routing chain for a single conversation. Routers set the ownership and
/// assignment on a <b>new</b> conversation; an existing thread keeps its assignment.
/// </summary>
public sealed class SmsRoutingContext
{
    /// <summary>
    /// Gets what caused this routing pass. A router that cannot see the trigger has to infer it, and the three
    /// paths that used to route independently each inferred it differently.
    /// </summary>
    public SmsRoutingTrigger Trigger { get; init; } = SmsRoutingTrigger.Inbound;

    /// <summary>
    /// Gets the message being routed (already normalized). Null for a pass with no new message, such as the
    /// unpicked-thread sweep.
    /// </summary>
    public OmnichannelMessage Message { get; init; }

    /// <summary>
    /// Gets the channel endpoint (DID) the conversation belongs to.
    /// </summary>
    public OmnichannelChannelEndpoint Endpoint { get; init; }

    /// <summary>
    /// Gets or sets the conversation being routed (found or created before the chain runs).
    /// </summary>
    public required SmsConversation Conversation { get; set; }

    /// <summary>
    /// Gets a value indicating whether the conversation was created for this pass (no prior thread existed).
    /// </summary>
    public bool IsNewConversation { get; init; }

    /// <summary>
    /// Gets or sets the agent the previous placement chose, so a reassignment can avoid handing the thread back
    /// to whoever did not pick it up.
    /// </summary>
    public string ExcludeAgentId { get; set; }

    /// <summary>
    /// Gets the queue an escalation or manual transfer is aimed at. Inbound routing takes its target from the
    /// endpoint instead, so this is null there.
    /// </summary>
    public string TargetQueueId { get; init; }

    /// <summary>
    /// Gets how many times a routed thread may be placed again before it falls back to the shared pool, so a
    /// thread successive agents ignore cannot bounce forever.
    /// </summary>
    public int MaxReassignmentAttempts { get; init; }

    /// <summary>
    /// Gets or sets the router that claimed the conversation, or <see langword="null"/> when none did. Set by
    /// the router, not by the chain members.
    /// </summary>
    public ISmsInboundRouter ClaimedBy { get; set; }
}
