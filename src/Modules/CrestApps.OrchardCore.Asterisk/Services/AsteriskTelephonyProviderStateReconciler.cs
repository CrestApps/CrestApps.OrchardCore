using CrestApps.OrchardCore.Telephony;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskTelephonyProviderStateReconciler : IAsteriskProviderStateReconciler
{
    private readonly ITelephonyInteractionSynchronizationService _synchronizationService;

    public AsteriskTelephonyProviderStateReconciler(
        ITelephonyInteractionSynchronizationService synchronizationService)
    {
        _synchronizationService = synchronizationService;
    }

    public async Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default)
    {
        await _synchronizationService.ReconcileProviderInteractionsAsync(providerName, cancellationToken);
    }
}
