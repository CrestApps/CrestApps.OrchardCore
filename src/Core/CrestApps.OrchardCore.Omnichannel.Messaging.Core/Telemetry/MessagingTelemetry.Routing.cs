using System.Diagnostics;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.Routers;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;

/// <summary>
/// The measurements of routing and assignment: the routing passes, the changes of a conversation's assignment, the
/// conversations placed again because nobody picked them up, and the first-response target.
/// </summary>
public static partial class MessagingTelemetry
{
    /// <summary>
    /// Records a routing pass and the router that claimed the conversation.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="trigger">What caused the pass.</param>
    /// <param name="router">The router that claimed the conversation, or <see langword="null"/> when none did.</param>
    public static void RecordRoute(string channel, MessagingRoutingTrigger trigger, IMessagingInboundRouter router)
    {
        if (!Route.Enabled)
        {
            return;
        }

        Add(Route, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.TriggerTag, GetTriggerTag(trigger) },
            { RouterTag, GetRouterTag(router) },
        });
    }

    /// <summary>
    /// Records a change of a conversation's assignment.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="trigger">What changed it: a routing trigger from <see cref="GetTriggerTag(MessagingRoutingTrigger)"/>,
    /// or one of <see cref="AssignmentTriggers"/>.</param>
    /// <param name="status">The assignment status the conversation ended in.</param>
    public static void RecordAssignment(string channel, string trigger, ConversationAssignmentStatus status)
    {
        if (!Assignment.Enabled)
        {
            return;
        }

        Add(Assignment, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.TriggerTag, trigger ?? CrestAppsOrchardCoreTelemetry.Unknown },
            { StatusTag, GetAssignmentStatusTag(status) },
        });
    }

    /// <summary>
    /// Records a conversation that missed its first-response target.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    public static void RecordSlaBreached(string channel)
    {
        if (!SlaBreached.Enabled)
        {
            return;
        }

        Add(SlaBreached, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
        });
    }

    /// <summary>
    /// Records a routed conversation placed again because nobody picked it up in time.
    /// </summary>
    /// <param name="channel">The technical name of the channel.</param>
    /// <param name="status">The assignment status the conversation ended in.</param>
    public static void RecordReassigned(string channel, ConversationAssignmentStatus status)
    {
        if (!Reassigned.Enabled)
        {
            return;
        }

        var result = status switch
        {
            ConversationAssignmentStatus.Assigned => ReassignmentResults.Rerouted,
            ConversationAssignmentStatus.Pooled => ReassignmentResults.Pooled,
            _ => ReassignmentResults.Unassigned,
        };

        Add(Reassigned, new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(channel) },
            { CrestAppsOrchardCoreTelemetry.ResultTag, result },
        });
    }

    /// <summary>
    /// Records the first reply a person sent on a conversation. Call it before the reply is marked on the conversation,
    /// so the conversation still says whether a first-response target was running.
    /// </summary>
    /// <param name="conversation">The conversation, not yet marked as responded.</param>
    /// <param name="respondedUtc">When the reply was sent.</param>
    public static void RecordFirstResponse(MessagingConversation conversation, DateTime respondedUtc)
    {
        ArgumentNullException.ThrowIfNull(conversation);

        if (!FirstResponseDuration.Enabled ||
            conversation.FirstRespondedUtc is not null ||
            conversation.CreatedUtc == default ||
            respondedUtc < conversation.CreatedUtc)
        {
            return;
        }

        var sla = conversation.FirstResponseBreached
            ? "breached"
            : conversation.FirstResponseDueUtc is not null
                ? "met"
                : "none";

        var tags = new TagList
        {
            { CrestAppsOrchardCoreTelemetry.ChannelTag, GetChannelTag(conversation.Channel) },
            { SlaTag, sla },
        };

        CrestAppsOrchardCoreTelemetry.AddTenantTag(ref tags);
        FirstResponseDuration.Record((respondedUtc - conversation.CreatedUtc).TotalSeconds, tags);
    }

    /// <summary>
    /// Gets the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> value of a routing trigger.
    /// </summary>
    /// <param name="trigger">The routing trigger.</param>
    /// <returns><c>inbound</c>, <c>handoff</c>, <c>reassignment</c>, <c>manual_transfer</c> or <c>other</c>.</returns>
    public static string GetTriggerTag(MessagingRoutingTrigger trigger)
    {
        return trigger switch
        {
            MessagingRoutingTrigger.Inbound => "inbound",
            MessagingRoutingTrigger.Handoff => "handoff",
            MessagingRoutingTrigger.Reassignment => "reassignment",
            MessagingRoutingTrigger.ManualTransfer => "manual_transfer",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets the <see cref="RouterTag"/> value of a router. A router another module adds is recorded as <c>other</c>.
    /// </summary>
    /// <param name="router">The router, or <see langword="null"/> when none claimed the conversation.</param>
    /// <returns>The tag value.</returns>
    public static string GetRouterTag(IMessagingInboundRouter router)
    {
        return router switch
        {
            null => "none",
            AutoReplyRouter => "auto_reply",
            ReassignmentRouter => "reassignment",
            HandoffQueueRouter => "handoff_queue",
            ExistingConversationRouter => "existing_conversation",
            RoutedQueueRouter => "routed_queue",
            EndpointRouteRouter => "endpoint_route",
            FallbackRouter => "fallback",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// Gets the <see cref="StatusTag"/> value of an assignment status.
    /// </summary>
    /// <param name="status">The assignment status.</param>
    /// <returns><c>unassigned</c>, <c>assigned</c>, <c>pooled</c> or <c>other</c>.</returns>
    public static string GetAssignmentStatusTag(ConversationAssignmentStatus status)
    {
        return status switch
        {
            ConversationAssignmentStatus.Unassigned => "unassigned",
            ConversationAssignmentStatus.Assigned => "assigned",
            ConversationAssignmentStatus.Pooled => "pooled",
            _ => CrestAppsOrchardCoreTelemetry.Other,
        };
    }

    /// <summary>
    /// The values of the <see cref="CrestAppsOrchardCoreTelemetry.TriggerTag"/> of <see cref="Assignment"/> that are not
    /// routing triggers.
    /// </summary>
    public static class AssignmentTriggers
    {
        /// <summary>
        /// An agent claimed the conversation.
        /// </summary>
        public const string Claim = "claim";

        /// <summary>
        /// An agent took the conversation by replying on it while nobody held it.
        /// </summary>
        public const string Reply = "reply";

        /// <summary>
        /// A supervisor or an integration assigned the conversation to an agent.
        /// </summary>
        public const string Assign = "assign";

        /// <summary>
        /// An agent or a supervisor transferred the conversation to a queue or an agent.
        /// </summary>
        public const string Transfer = "transfer";
    }

    /// <summary>
    /// The results of <see cref="Reassigned"/>.
    /// </summary>
    public static class ReassignmentResults
    {
        /// <summary>
        /// The conversation was routed to another agent.
        /// </summary>
        public const string Rerouted = "rerouted";

        /// <summary>
        /// The conversation was returned to its queue's shared pool.
        /// </summary>
        public const string Pooled = "pooled";

        /// <summary>
        /// The conversation was left unassigned.
        /// </summary>
        public const string Unassigned = "unassigned";
    }
}
