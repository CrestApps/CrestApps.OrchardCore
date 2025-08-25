using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Resources;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("easymde")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/js/easymde.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/js/easymde.js"
            )
            .SetVersion("2.18.0");

        _manifest
            .DefineStyle("easymde")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/css/easymde.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/easymde/css/easymde.css"
            )
            .SetVersion("2.18.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
