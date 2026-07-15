using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Extensions.Features;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.ContactCenter.Services;

internal sealed class ContactCenterFeatureLifecycleHandler : FeatureEventHandler
{
    private readonly IServiceProvider _serviceProvider;

    public ContactCenterFeatureLifecycleHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public override Task DisablingAsync(IFeatureInfo feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        return _serviceProvider
            .GetRequiredService<ContactCenterFeatureLifecycleCoordinator>()
            .QuiesceAsync(feature.Id);
    }
}
