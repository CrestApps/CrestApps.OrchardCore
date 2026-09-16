namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;

/// <summary>
/// The single entry point for deciding who owns an SMS conversation. Inbound delivery, escalation from an
/// automated conversation, the unpicked-thread sweep and a manual transfer all go through it, so a change to the
/// ownership rules reaches every path instead of one of them.
/// </summary>
public interface ISmsConversationRouter
{
    /// <summary>
    /// Runs the routing chain over the supplied context and returns it, with <see cref="SmsRoutingContext.ClaimedBy"/>
    /// set to the router that claimed the conversation, if any.
    /// </summary>
    /// <param name="context">The routing context.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The same context, after the chain has run.</returns>
    Task<SmsRoutingContext> RouteAsync(SmsRoutingContext context, CancellationToken cancellationToken = default);
}
