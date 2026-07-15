using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureDisablingHandler : FeatureEventHandler
{
    private readonly ContactCenterFeatureLifecycleCoordinator _coordinator;

    public ContactCenterFeatureDisablingHandler(ContactCenterFeatureLifecycleCoordinator coordinator)
    {
        _coordinator = coordinator;
    }

    public override Task DisablingAsync(IFeatureInfo feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        return _coordinator.QuiesceAsync(feature.Id);
    }
}
