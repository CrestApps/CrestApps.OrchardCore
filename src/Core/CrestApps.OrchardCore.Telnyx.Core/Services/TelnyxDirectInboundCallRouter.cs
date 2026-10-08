namespace CrestApps.OrchardCore.Telnyx.Services;

/// <summary>
/// The base-feature inbound router. Telnyx authenticates with a single tenant API key rather than a
/// per-user identity, so the base feature has no way to attribute an inbound DID to a specific soft-phone
/// user on its own. Inbound routing to agents is provided by the Telnyx Contact Center Voice feature, which
/// replaces this router with one that routes through the Contact Center entry-point front door. This
/// implementation therefore never routes a call.
/// </summary>
public sealed class TelnyxDirectInboundCallRouter : ITelnyxInboundCallRouter
{
    /// <inheritdoc/>
    public Task<bool> RouteAsync(
        TelnyxCallEvent callEvent,
        DateTime occurredUtc,
        CancellationToken cancellationToken = default)
        => Task.FromResult(false);
}
