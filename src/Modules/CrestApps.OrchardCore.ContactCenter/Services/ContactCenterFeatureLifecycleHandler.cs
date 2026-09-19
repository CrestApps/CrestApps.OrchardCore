using CrestApps.Core.ContactCenter;
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

    public override async Task DisablingAsync(IFeatureInfo feature)
    {
        ArgumentNullException.ThrowIfNull(feature);

        var coordinator = _serviceProvider.GetRequiredService<ContactCenterFeatureLifecycleCoordinator>();

        // A feature may own more than one capability, and a capability may be owned by more than one
        // feature - a provider contributes voice work that both its own feature and the Contact Center
        // voice feature are entitled to drain.
        var capabilities = _serviceProvider
            .GetServices<ContactCenterFeatureCapabilityMapping>()
            .Where(mapping => string.Equals(mapping.FeatureId, feature.Id, StringComparison.Ordinal))
            .Select(mapping => mapping.Capability)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // A feature nobody mapped keeps the old behaviour of matching participants by its own id, so a
        // participant added later without a mapping still drains instead of silently surviving the
        // disable.
        if (capabilities.Count == 0)
        {
            capabilities.Add(feature.Id);
        }

        await coordinator.QuiesceAsync(capabilities);
    }
}
