using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.Services;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("OpenAIChatApp")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/ai-chat.js")
            .SetDependencies("vuejs:3", "signalr", "marked")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("marked")
            .SetUrl("~/CrestApps.OrchardCore.AI.Chat/scripts/marked.min.js", "~/CrestApps.OrchardCore.AI.Chat/scripts/marked.js")
            .SetCdn("https://cdnjs.cloudflare.com/ajax/libs/marked/15.0.6/marked.min.js", "https://cdnjs.cloudflare.com/ajax/libs/marked/15.0.6/marked.min.js")
            .SetCdnIntegrity("sha512-rvRITpPeEKe4hV9M8XntuXX6nuohzqdR5O3W6nhjTLwkrx0ZgBQuaK4fv5DdOWzs2IaXsGt5h0+nyp9pEuoTXg==", "sha512-rvRITpPeEKe4hV9M8XntuXX6nuohzqdR5O3W6nhjTLwkrx0ZgBQuaK4fv5DdOWzs2IaXsGt5h0+nyp9pEuoTXg==")
            .SetVersion("15.0.6");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
