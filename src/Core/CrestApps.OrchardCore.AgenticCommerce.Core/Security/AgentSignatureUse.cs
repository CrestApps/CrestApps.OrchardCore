namespace CrestApps.OrchardCore.AgenticCommerce.Core.Security;

/// <summary>
/// Whether <see cref="AgentSignatureReplayGuard"/> lets a verified signature be used.
/// </summary>
public enum AgentSignatureUse
{
    /// <summary>
    /// The signature wasn't used before, or it is the same idempotent request again: it is accepted.
    /// </summary>
    Accepted,

    /// <summary>
    /// The signature was used before by a request that isn't the same idempotent one: it is refused.
    /// </summary>
    Replayed,

    /// <summary>
    /// The signature's lock couldn't be taken in time (the lock store is down or busy), so whether it was used before
    /// is unknown. The request may be sent again.
    /// </summary>
    Unavailable,
}
