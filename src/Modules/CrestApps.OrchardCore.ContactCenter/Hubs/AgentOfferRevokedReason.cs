namespace CrestApps.OrchardCore.ContactCenter.Hubs;

/// <summary>
/// Identifies why an offer presented to an agent was revoked.
/// </summary>
public enum AgentOfferRevokedReason
{
    /// <summary>
    /// The reservation expired before the agent accepted it.
    /// </summary>
    Expired,

    /// <summary>
    /// The offer was released back to the queue, for example after a decline or cancellation.
    /// </summary>
    Released,

    /// <summary>
    /// The offer was accepted, so the pending offer should be cleared.
    /// </summary>
    Accepted,
}
