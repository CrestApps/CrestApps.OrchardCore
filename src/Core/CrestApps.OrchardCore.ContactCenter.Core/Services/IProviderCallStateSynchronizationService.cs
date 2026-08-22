using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Synchronizes Contact Center interaction state with authoritative provider call state.
/// </summary>
public interface IProviderCallStateSynchronizationService
{
    /// <summary>
    /// Refreshes the specified interaction from the provider's current call state when the provider
    /// supports state lookups.
    /// </summary>
    /// <param name="interaction">The interaction to refresh.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The refreshed interaction.</returns>
    Task<Interaction> RefreshInteractionAsync(Interaction interaction, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles all active provider-backed interactions against the current provider state.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of interactions whose provider state was refreshed.</returns>
    Task<int> ReconcileActiveInteractionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reconciles active provider-backed interactions for the specified provider against its current call state.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of interactions whose provider state was refreshed.</returns>
    Task<int> ReconcileProviderInteractionsAsync(string providerName, CancellationToken cancellationToken = default);
}
