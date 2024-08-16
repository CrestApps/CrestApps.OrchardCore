using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

public sealed class SubscriptionResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static SubscriptionResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("subscription-payment-methods")
            .SetUrl("~/CrestApps.OrchardCore.Subscriptions/Scripts/payment-option-selection.min.js", "~/CrestApps.OrchardCore.Subscriptions/Scripts/payment-option-selection.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
