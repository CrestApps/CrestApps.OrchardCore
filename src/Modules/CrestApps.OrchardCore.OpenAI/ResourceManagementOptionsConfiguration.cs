using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.OpenAI;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("OpenAIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.OpenAI/Scripts/OpenAI-chat.min.js", "~/CrestApps.OrchardCore.OpenAI/Scripts/OpenAI-chat.js")
            .SetDependencies("vuejs:3")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
