using CrestApps.Core.ContactCenter;

namespace CrestApps.Core.Telephony.Asterisk.Services;

public sealed class AsteriskContactCenterProviderStateReconciler : IAsteriskProviderStateReconciler
{
    private readonly IProviderCallStateReconciler _reconciler;

    public AsteriskContactCenterProviderStateReconciler(
        IProviderCallStateReconciler reconciler)
    {
        _reconciler = reconciler;
    }

    public async Task ReconcileAsync(string providerName, CancellationToken cancellationToken = default)
    {
        await _reconciler.ReconcileAsync(providerName, cancellationToken);
    }
}
