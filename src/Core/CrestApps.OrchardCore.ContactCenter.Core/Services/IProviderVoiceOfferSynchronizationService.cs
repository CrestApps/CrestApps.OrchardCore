namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles queue, reservation, and agent offer state when provider truth shows that a pre-connect
/// voice offer has already ended.
/// </summary>
public interface IProviderVoiceOfferSynchronizationService
{
    /// <summary>
    /// Clears stale pre-connect offer state for the specified interaction when the provider has already
    /// ended the call before it was authoritatively connected.
    /// </summary>
    /// <param name="interactionId">The interaction identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReconcileEndedOfferAsync(string interactionId, CancellationToken cancellationToken = default);
}
