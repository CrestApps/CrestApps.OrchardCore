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
            .DefineScript("vuejs")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/vue.global.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/vue.global.js"
            )
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/vue@3.5.13/dist/vue.global.prod.min.js",
                "https://cdn.jsdelivr.net/npm/vue@3.5.13/dist/vue.global.js"
            )
            .SetCdnIntegrity(
                "sha384-ZvVvvjBwvU29cD0yQLwh8++Sa0uYooNo1jVSRV0aSSmDWm+hYxokwYXmmEzu4ZTS",
                "sha384-G++pO/TtP6SeNEBuO/CYuppmlcEhA0Rj9IcY5feVJXhyYraEA8CKVZV38iDXLTyJ"
            )
            .SetVersion("3.5.13");

        _manifest
            .DefineScript("font-awesome")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/fontawesome-free/js/all.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/fontawesome-free/js/all.js"
            )
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.7.2/js/all.min.js",
                "https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.7.2/js/all.js"
            )
            .SetCdnIntegrity(
                "sha384-DsXFqEUf3HnCU8om0zbXN58DxV7Bo8/z7AbHBGd2XxkeNpdLrygNiGFr/03W0Xmt",
                "sha384-103HZqplx8RDtihZoKY8x3qZcFKEwjwT7B2gSWIPsHW3Bw+oZ/YuC4ZG2NCs9X2l"
            )
            .SetVersion("6.7.2");

        _manifest
            .DefineScript("font-awesome-v4-shims")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/fontawesome-free/js/v4-shims.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/fontawesome-free/js/v4-shims.js"
            )
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.7.2/js/v4-shims.min.js",
                "https://cdn.jsdelivr.net/npm/@fortawesome/fontawesome-free@6.7.2/js/v4-shims.js"
            )
            .SetCdnIntegrity(
                "sha384-WVm8++sQXsfFD5HmhLau6q7RS11CQOYMBHGi1pfF2PHd/vthiacQvsVLrRk6lH8O",
                "sha384-8wHa6NoZT1zIIflbE6bEpvkCitRAeXbtoIAZAaddda+A7iyDB1/WHrGFXXXOqRzp"
            )
            .SetVersion("6.7.2");

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
