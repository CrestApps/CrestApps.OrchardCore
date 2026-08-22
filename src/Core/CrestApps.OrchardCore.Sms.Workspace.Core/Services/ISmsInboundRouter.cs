namespace CrestApps.OrchardCore.Sms.Workspace.Core.Services;

/// <summary>
/// One link in the ordered inbound routing chain. Implementations decide who owns a newly received
/// conversation (an agent's personal inbox, a queue's shared pool, or the unassigned inbox). The chain runs in
/// ascending <see cref="Order"/>; the first router that returns <see langword="true"/> stops the chain.
/// </summary>
public interface ISmsInboundRouter
{
    /// <summary>
    /// Gets the order this router runs in. Lower runs first.
    /// </summary>
    int Order { get; }

    /// <summary>
    /// Attempts to route the inbound message. Returns <see langword="true"/> when this router has taken
    /// responsibility for the conversation and the chain should stop.
    /// </summary>
    /// <param name="context">The inbound routing state.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when handled; otherwise <see langword="false"/> to continue the chain.</returns>
    Task<bool> TryRouteAsync(SmsInboundRoutingContext context, CancellationToken cancellationToken = default);
}
