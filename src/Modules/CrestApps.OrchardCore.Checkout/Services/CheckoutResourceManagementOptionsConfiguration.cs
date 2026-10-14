using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Checkout.Services;

/// <summary>
/// Registers the client-side resources used by the public checkout.
/// </summary>
public sealed class CheckoutResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static CheckoutResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("checkout-payment")
            .SetUrl("~/CrestApps.OrchardCore.Checkout/Scripts/checkout-payment.min.js", "~/CrestApps.OrchardCore.Checkout/Scripts/checkout-payment.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
