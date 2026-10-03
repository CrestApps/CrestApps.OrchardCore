using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;

/// <summary>
/// Where messages to one of the business's addresses go, as the inbound entry point that answers the address says at
/// the moment a message arrives.
/// </summary>
public sealed class MessagingInboundRouting
{
    /// <summary>
    /// Gets the entry point the routing comes from.
    /// </summary>
    public string EntryPointId { get; init; }

    /// <summary>
    /// Gets whether the messages go to an agent or a queue.
    /// </summary>
    public ConversationRouteTargetType TargetType { get; init; }

    /// <summary>
    /// Gets the agent profile or queue the messages go to.
    /// </summary>
    public string TargetId { get; init; }

    /// <summary>
    /// Gets how a queue's conversations are handed out.
    /// </summary>
    public ConversationDistributionMode DistributionMode { get; init; }

    /// <summary>
    /// Gets a value indicating whether the entry point is open now.
    /// </summary>
    public bool IsOpen { get; init; } = true;

    /// <summary>
    /// Gets the automatic reply owed now: the closed reply while closed, when there is one, and otherwise the ordinary
    /// one. Empty when there is none.
    /// </summary>
    public string AutoReplyMessage { get; init; }
}
