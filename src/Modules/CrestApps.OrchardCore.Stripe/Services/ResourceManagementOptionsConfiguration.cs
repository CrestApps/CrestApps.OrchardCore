using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("stripe")
            .SetCdn("https://js.stripe.com/v3/")
            .SetVersion("16.6.0");

        _manifest
            .DefineScript("stripe-subscription-processor")
            .SetUrl("~/CrestApps.OrchardCore.Stripe/Scripts/stripe-subscription-payment-processing.min.js", "~/CrestApps.OrchardCore.Stripe/Scripts/stripe-subscription-payment-processing.js")
            .SetDependencies("stripe")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
