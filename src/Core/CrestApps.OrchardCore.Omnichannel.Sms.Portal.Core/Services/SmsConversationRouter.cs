using CrestApps.Core.Support;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// Runs the ordered <see cref="ISmsInboundRouter"/> chain and stops at the first router that claims the
/// conversation. When none claims it the thread is left unassigned, which keeps it visible and claimable rather
/// than silently attached to whichever router happened to run last.
/// </summary>
public sealed class SmsConversationRouter : ISmsConversationRouter
{
    private readonly ISmsInboundRouter[] _routers;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SmsConversationRouter"/> class.
    /// </summary>
    /// <param name="routers">The routing chain.</param>
    /// <param name="logger">The logger.</param>
    public SmsConversationRouter(IEnumerable<ISmsInboundRouter> routers, ILogger<SmsConversationRouter> logger)
    {
        _routers = routers.OrderBy(router => router.Order).ToArray();
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<SmsRoutingContext> RouteAsync(SmsRoutingContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

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
                "No SMS router claimed conversation {ConversationId} for trigger {Trigger}; it stays unassigned.",
                context.Conversation.ItemId.SanitizeLogValue(),
                context.Trigger);
        }

        return context;
    }
}
