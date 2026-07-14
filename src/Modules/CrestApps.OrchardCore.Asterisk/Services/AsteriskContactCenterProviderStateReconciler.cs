using CrestApps.OrchardCore.ContactCenter.Core.Services;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskContactCenterProviderStateReconciler : IAsteriskProviderStateReconciler
{
    private readonly IProviderCallStateSynchronizationService _synchronizationService;

    public AsteriskContactCenterProviderStateReconciler(
        IProviderCallStateSynchronizationService synchronizationService)
    {
        _synchronizationService = synchronizationService;
    }

    public async Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default)
    {
        await _synchronizationService.ReconcileProviderInteractionsAsync(providerName, cancellationToken);
    }
}
