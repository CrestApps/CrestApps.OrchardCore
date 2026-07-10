using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Telephony;

/// <summary>
/// Reconciles locally persisted telephony interactions with provider-authoritative call state.
/// </summary>
public interface ITelephonyInteractionSynchronizationService
{
    /// <summary>
    /// Gets the current provider-authoritative call for a user.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The provider call lookup result.</returns>
    Task<TelephonyCallLookupResult> GetActiveCallAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles all active telephony interactions.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of interactions whose persisted state changed.</returns>
    Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles active telephony interactions for one provider.
    /// </summary>
    /// <param name="providerName">The technical provider name.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of interactions whose persisted state changed.</returns>
    Task<int> ReconcileProviderInteractionsAsync(string providerName, CancellationToken cancellationToken = default);
}
