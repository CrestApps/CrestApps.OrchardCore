namespace CrestApps.OrchardCore.ContactCenter;

/// <summary>
/// Reconciles Contact Center call state against authoritative provider state.
/// </summary>
public interface IProviderCallStateReconciler
{
    /// <summary>
    /// Reconciles active interactions for the specified provider.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The number of interactions whose state was refreshed.</returns>
    Task<int> ReconcileAsync(
        string providerName,
        CancellationToken cancellationToken = default);
}
