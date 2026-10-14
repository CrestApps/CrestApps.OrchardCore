using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Carts.Services;

/// <summary>
/// Registers the client-side resources of the cart.
/// </summary>
public sealed class CartResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static CartResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // One list per option for a product sold in variants. The form works without it, with one list of variants.
        _manifest
            .DefineScript("product-variant-picker")
            .SetUrl("~/CrestApps.OrchardCore.Carts/Scripts/variant-picker.min.js", "~/CrestApps.OrchardCore.Carts/Scripts/variant-picker.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
