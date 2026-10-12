using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Commerce.Services;

/// <summary>
/// Registers the client-side resources shared by the commerce admin screens.
/// </summary>
public sealed class CommerceResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static CommerceResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // The header filters of the commerce admin lists.
        _manifest
            .DefineScript("commerce-admin-list")
            .SetUrl("~/CrestApps.OrchardCore.Commerce/Scripts/commerce-admin-list.min.js", "~/CrestApps.OrchardCore.Commerce/Scripts/commerce-admin-list.js")
            .SetDependencies("bootstrap-select")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
