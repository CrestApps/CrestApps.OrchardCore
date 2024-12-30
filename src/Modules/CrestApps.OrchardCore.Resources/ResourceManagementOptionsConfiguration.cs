using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Resources;

public sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript("vuejs")
            .SetUrl("~/CrestApps.OrchardCore.Resources/Scripts/vue.global.min.js", "~/CrestApps.OrchardCore.Resources/Scripts/vue.global.js")
            .SetCdn("https://cdn.jsdelivr.net/npm/vue@3.5.13/dist/vue.global.prod.min.js", "https://cdn.jsdelivr.net/npm/vue@3.5.13/dist/vue.global.js")
            .SetCdnIntegrity("sha384-ZvVvvjBwvU29cD0yQLwh8++Sa0uYooNo1jVSRV0aSSmDWm+hYxokwYXmmEzu4ZTS", "sha384-G++pO/TtP6SeNEBuO/CYuppmlcEhA0Rj9IcY5feVJXhyYraEA8CKVZV38iDXLTyJ")
            .SetVersion("3.5.13");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
