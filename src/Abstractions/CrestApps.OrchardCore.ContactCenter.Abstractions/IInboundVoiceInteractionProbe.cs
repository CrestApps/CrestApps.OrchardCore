namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Probes whether a durable, still-active Contact Center interaction exists for a provider call, so a provider can
/// recover a caller leg forward instead of tearing down a live, routed call.
/// </summary>
public interface IInboundVoiceInteractionProbe
{
    /// <summary>
    /// Determines whether an active (non-terminal) interaction exists for the specified provider call.
    /// </summary>
    /// <param name="providerName">The provider technical name that owns the call.</param>
    /// <param name="providerCallId">The provider-assigned call identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="true"/> when a durable, non-terminal interaction exists; otherwise, <see langword="false"/>.</returns>
    Task<bool> HasActiveInteractionAsync(
        string providerName,
        string providerCallId,
        CancellationToken cancellationToken = default);
}
