using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.ContentTransfer.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("ContentTransferImport")
            .SetUrl(
                "~/CrestApps.OrchardCore.ContentTransfer/scripts/content-transfer-import.min.js",
                "~/CrestApps.OrchardCore.ContentTransfer/scripts/content-transfer-import.js")
            .SetVersion("1.0.0");
    }

    /// <summary>
    /// Adds the Content Transfer resource manifest to the resource management options.
    /// </summary>
    /// <param name="options">The resource management options to configure.</param>
    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
