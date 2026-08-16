using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Registers the client-side resources used by subscription payment flows.
/// </summary>
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

        _manifest
            .DefineScript("stripe-subscription-checkout")
            .SetUrl("~/CrestApps.OrchardCore.Subscriptions/Scripts/stripe-subscription-checkout.min.js", "~/CrestApps.OrchardCore.Subscriptions/Scripts/stripe-subscription-checkout.js")
            .SetDependencies("subscription-payment-methods")
            .SetVersion("1.0.0");
    }

    /// <summary>
    /// Adds the subscription resource manifest to the Orchard Core resource management options.
    /// </summary>
    /// <param name="options">The resource management options to configure.</param>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
