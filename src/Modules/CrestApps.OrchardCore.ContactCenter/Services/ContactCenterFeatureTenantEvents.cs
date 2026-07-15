using OrchardCore.Modules;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureTenantEvents : ModularTenantEvents
{
    private readonly ContactCenterFeatureLifecycleCoordinator _coordinator;

    public ContactCenterFeatureTenantEvents(ContactCenterFeatureLifecycleCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public override Task ActivatingAsync()
    {
        return _coordinator.ReconcileAsync();
    }
}
