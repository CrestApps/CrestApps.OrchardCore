using CrestApps.Core.Telephony;

namespace CrestApps.Core.Telephony.Asterisk.Services;

public sealed class AsteriskTelephonyProviderStateReconciler : IAsteriskProviderStateReconciler
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
