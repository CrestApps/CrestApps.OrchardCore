using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Resources;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        ResourcesForBackwardCompatibility();

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

    /// <summary>
    /// These resources were added to keep compatibility between OrchardCore v2 and v3.
    /// Don't update the version.
    /// </summary>
    private static void ResourcesForBackwardCompatibility()
    {
        _manifest
            .DefineScript("vuejs")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/vue-2.6.14/vue.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/vue-2.6.14/vue.js"
            )
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/vue@2.6.14/dist/vue.min.js",
                "https://cdn.jsdelivr.net/npm/vue@2.6.14/dist/vue.js"
            )
            .SetCdnIntegrity(
                "sha384-ULpZhk1pvhc/UK5ktA9kwb2guy9ovNSTyxPNHANnA35YjBQgdwI+AhLkixDvdlw4",
                "sha384-t1tHLsbM7bYMJCXlhr0//00jSs7ZhsAhxgm191xFsyzvieTMCbUWKMhFg9I6ci8q"
            )
            .SetVersion("2.0.0");

        _manifest
            .DefineScript("vue-draggable")
            .SetDependencies("vuejs:2", "Sortable")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/vue-draggable-2.24.3/vuedraggable.umd.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/vue-draggable-2.24.3/vuedraggable.umd.js"
            )
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/vuedraggable@2.24.3/dist/vuedraggable.umd.min.js",
                "https://cdn.jsdelivr.net/npm/vuedraggable@2.24.3/dist/vuedraggable.umd.js"
            )
            .SetCdnIntegrity(
                "sha384-qUA1xXJiX23E4GOeW/XHtsBkV9MUcHLSjhi3FzO08mv8+W8bv5AQ1cwqLskycOTs",
                "sha384-+jB9vXc/EaIJTlNiZG2tv+TUpKm6GR9HCRZb3VkI3lscZWqrCYDbX2ZXffNJldL9"
            )
            .SetVersion("2.0.0");

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
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
