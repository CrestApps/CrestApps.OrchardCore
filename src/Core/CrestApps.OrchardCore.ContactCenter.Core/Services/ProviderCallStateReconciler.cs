namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Adapts provider-facing reconciliation to the Contact Center synchronization service.
/// </summary>
public sealed class ProviderCallStateReconciler : IProviderCallStateReconciler
{
    private readonly IProviderCallStateSynchronizationService _synchronizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderCallStateReconciler"/> class.
    /// </summary>
    /// <param name="synchronizationService">The Contact Center provider-state synchronization service.</param>
    public ProviderCallStateReconciler(IProviderCallStateSynchronizationService synchronizationService)
    {
        _synchronizationService = synchronizationService;
    }

    /// <inheritdoc/>
    public Task<int> ReconcileAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        return _synchronizationService.ReconcileProviderInteractionsAsync(providerName, cancellationToken);
    }
}
