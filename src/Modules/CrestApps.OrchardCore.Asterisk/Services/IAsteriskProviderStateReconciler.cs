namespace CrestApps.OrchardCore.Asterisk.Services;

/// <summary>
/// Reconciles provider-backed state after an Asterisk real-time listener reconnects.
/// </summary>
internal interface IAsteriskProviderStateReconciler
{
    /// <summary>
    /// Reconciles state for the specified provider.
    /// </summary>
    /// <param name="providerName">The provider technical name.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default);
}
