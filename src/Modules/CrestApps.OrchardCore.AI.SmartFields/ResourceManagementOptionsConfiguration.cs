using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.SmartFields;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("smart-text-field")
            .SetUrl("~/CrestApps.OrchardCore.AI.SmartFields/scripts/smart-text-field.min.js", "~/CrestApps.OrchardCore.AI.SmartFields/scripts/smart-text-field.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("smart-text-field")
            .SetUrl("~/CrestApps.OrchardCore.AI.SmartFields/styles/smart-text-field.min.css", "~/CrestApps.OrchardCore.AI.SmartFields/styles/smart-text-field.css")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
