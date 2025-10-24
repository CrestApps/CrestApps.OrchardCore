using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("ai-admin-ui")
            .SetUrl("~/CrestApps.OrchardCore.AI/scripts/admin-ui.min.js", "~/CrestApps.OrchardCore.AI/scripts/admin-ui.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("technical-name-generator")
            .SetUrl("~/CrestApps.OrchardCore.AI/scripts/technical-name-generator.min.js", "~/CrestApps.OrchardCore.AI/scripts/technical-name-generator.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
