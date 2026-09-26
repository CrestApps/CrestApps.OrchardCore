using CrestApps.Core.Support;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
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
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingConversationRouter"/> class.
    /// </summary>
    /// <param name="routers">The routing chain.</param>
    /// <param name="channelResolver">The resolver of the enabled channels.</param>
    /// <param name="logger">The logger.</param>
    public MessagingConversationRouter(
        IEnumerable<IMessagingInboundRouter> routers,
        IMessagingChannelResolver channelResolver,
        ILogger<MessagingConversationRouter> logger)
    {
        _routers = routers.OrderBy(router => router.Order).ToArray();
        _channelResolver = channelResolver;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<MessagingRoutingContext> RouteAsync(MessagingRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Channel ??= _channelResolver.Get(context.Conversation.Channel);

        foreach (var router in _routers)
        {
            if (await router.TryRouteAsync(context, cancellationToken))
            {
                context.ClaimedBy = router;

                break;
            }
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
