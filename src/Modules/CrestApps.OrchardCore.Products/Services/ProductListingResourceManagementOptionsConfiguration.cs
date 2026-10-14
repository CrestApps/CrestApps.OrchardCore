using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Products.Services;

/// <summary>
/// Registers the client-side resources of the product listing pages.
/// </summary>
public sealed class ProductListingResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ProductListingResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // Applies filters as they change and updates the results without a full page load. The listing works
        // without it: the filter form is an ordinary GET form.
        _manifest
            .DefineScript("product-listing")
            .SetUrl("~/CrestApps.OrchardCore.Products/Scripts/product-listing.min.js", "~/CrestApps.OrchardCore.Products/Scripts/product-listing.js")
            .SetVersion("1.0.0");

        // The listing editor: the filters and sorts of the picked product types, dragged into order.
        _manifest
            .DefineScript("product-listing-editor")
            .SetUrl("~/CrestApps.OrchardCore.Products/Scripts/product-listing-editor.min.js", "~/CrestApps.OrchardCore.Products/Scripts/product-listing-editor.js")
            .SetDependencies("Sortable")
            .SetVersion("1.0.0");

        // The responsive filters panel and image cropping, for themes on a Bootstrap older than 5.3.
        _manifest
            .DefineStyle("product-listing")
            .SetUrl("~/CrestApps.OrchardCore.Products/Styles/product-listing.min.css", "~/CrestApps.OrchardCore.Products/Styles/product-listing.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
