using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Registers the client-side resources of the product editor.
/// </summary>
public sealed class ProductResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ProductResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // The price editor: adds a price row and shows the recurring fields for a recurring price.
        _manifest
            .DefineScript("product-price-editor")
            .SetUrl("~/CrestApps.OrchardCore.Products/Scripts/product-price-editor.min.js", "~/CrestApps.OrchardCore.Products/Scripts/product-price-editor.js")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
