using CrestApps.OrchardCore.ContactCenter;

namespace CrestApps.OrchardCore.Asterisk.Services;

internal sealed class AsteriskContactCenterProviderStateReconciler : IAsteriskProviderStateReconciler
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
