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
            .DefineScript("list-management-ui")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/list-management-ui.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/list-management-ui.js"
            )
            .SetVersion("1.0.0");

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

        _manifest
        .DefineScript("chart.js")
        .SetUrl(
            "~/CrestApps.OrchardCore.Resources/vendors/chartjs/chart.min.js",
            "~/CrestApps.OrchardCore.Resources/vendors/chartjs/chart.js")
        .SetCdn(
            "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/dist/chart.umd.min.js",
            "https://cdn.jsdelivr.net/npm/chart.js@4.5.1/dist/chart.umd.js")
        .SetCdnIntegrity(
            "sha384-jb8JQMbMoBUzgWatfe6COACi2ljcDdZQ2OxczGA3bGNeWe+6DChMTBJemed7ZnvJ",
            "sha384-hfkuqrKeWFmnTMWN31VWyoe8xgdTADD11kgxmdpx2uyE6j5Az5uZq6u6AKYYmAOw")
        .SetVersion("4.5.1");

        _manifest
            .DefineScript("marked")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/marked.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/marked.js")
            .SetCdn(
                "https://cdnjs.cloudflare.com/ajax/libs/marked/15.0.6/marked.min.js",
                "https://cdnjs.cloudflare.com/ajax/libs/marked/15.0.6/marked.min.js")
            .SetCdnIntegrity(
                "sha512-rvRITpPeEKe4hV9M8XntuXX6nuohzqdR5O3W6nhjTLwkrx0ZgBQuaK4fv5DdOWzs2IaXsGt5h0+nyp9pEuoTXg==",
                "sha512-rvRITpPeEKe4hV9M8XntuXX6nuohzqdR5O3W6nhjTLwkrx0ZgBQuaK4fv5DdOWzs2IaXsGt5h0+nyp9pEuoTXg==")
            .SetVersion("15.0.6");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
