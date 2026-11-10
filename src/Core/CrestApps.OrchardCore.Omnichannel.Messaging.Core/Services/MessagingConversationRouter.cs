using CrestApps.Core.Support;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Telemetry;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Runs the ordered <see cref="IMessagingInboundRouter"/> chain and stops at the first router that claims the
/// conversation. When none claims it the thread is left unassigned, which keeps it visible and claimable rather
/// than silently attached to whichever router happened to run last.
/// </summary>
public sealed class MessagingConversationRouter : IMessagingConversationRouter
{
    private readonly IMessagingInboundRouter[] _routers;
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IMessagingInboundRoutingResolver _routingResolver;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationRouter"/> class.
    /// </summary>
    /// <param name="routers">The routing chain.</param>
    /// <param name="channelResolver">The resolver of the enabled channels.</param>
    /// <param name="routingResolver">The resolver of where an endpoint's messages go.</param>
    /// <param name="logger">The logger.</param>
    public MessagingConversationRouter(
        IEnumerable<IMessagingInboundRouter> routers,
        IMessagingChannelResolver channelResolver,
        IMessagingInboundRoutingResolver routingResolver,
        ILogger<MessagingConversationRouter> logger)
    {
        _routers = routers.OrderBy(router => router.Order).ToArray();
        _channelResolver = channelResolver;
        _routingResolver = routingResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingRoutingContext> RouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Channel ??= _channelResolver.Get(context.Conversation.Channel);

        using var telemetry = TelemetryScope.Start(MessagingTelemetry.ActivitySource, "crestapps.messaging.route", duration: null);
        telemetry.SetSpanTag(CrestAppsOrchardCoreTelemetry.TriggerTag, MessagingTelemetry.GetTriggerTag(context.Trigger));
        telemetry.SetSpanTag(MessagingTelemetry.ConversationIdSpanTag, context.Conversation.ItemId);

        var previousAgentId = context.Conversation.AssignedAgentId;
        var previousStatus = context.Conversation.AssignmentStatus;

        try
        {
            // Read once for the whole chain, so every router sees the same entry point and the same open or closed answer.
            context.Routing ??= await _routingResolver.ResolveAsync(context.Endpoint, context.Conversation.Channel, cancellationToken);

            foreach (var router in _routers)
            {
                if (await router.TryRouteAsync(context, cancellationToken))
                {
                    context.ClaimedBy = router;

                    break;
                }
            }
        }
        catch (Exception ex)
        {
            // The type only, never the message: a routing error can quote the contact's address.
            telemetry.SetSpanTag(CrestAppsOrchardCoreTelemetry.ErrorTypeTag, ex.GetType().FullName);

            if (CrestAppsOrchardCoreTelemetry.GetOutcome(ex) == CrestAppsOrchardCoreTelemetry.OutcomeError)
            {
                telemetry.Fail();
            }
            else
            {
                telemetry.SetOutcome(CrestAppsOrchardCoreTelemetry.GetOutcome(ex));
            }

            throw;
        }

        var channel = context.Channel?.Name ?? context.Conversation.Channel;

        telemetry.SetSpanTag(MessagingTelemetry.RouterTag, MessagingTelemetry.GetRouterTag(context.ClaimedBy));
        telemetry.Succeed();
        MessagingTelemetry.RecordRoute(channel, context.Trigger, context.ClaimedBy);

        // A new thread is always placed somewhere, even when that is nowhere; an existing one counts only when it moved.
        if (context.IsNewConversation ||
            previousStatus != context.Conversation.AssignmentStatus ||
            !string.Equals(previousAgentId, context.Conversation.AssignedAgentId, StringComparison.Ordinal))
        {
            MessagingTelemetry.RecordAssignment(channel, MessagingTelemetry.GetTriggerTag(context.Trigger), context.Conversation.AssignmentStatus);
        }

        if (context.ClaimedBy is null && _logger.IsEnabled(LogLevel.Debug))
        {
            _logger.LogDebug(
                "No messaging router claimed conversation {ConversationId} for trigger {Trigger}; it stays unassigned.",
                context.Conversation.ItemId.SanitizeLogValue(),
                context.Trigger);
        }

        return context;
    }
}
