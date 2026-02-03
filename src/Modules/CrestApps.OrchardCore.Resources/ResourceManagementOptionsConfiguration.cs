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

        _manifest
            .DefineScript("flatpickr")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/js/flatpickr.min.js",
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/js/flatpickr.js")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.js",
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.js")
            .SetCdnIntegrity(
                "sha384-6MRMrUEhJMa1+Lu30o5HJn4S0FFOEKnFZFWDfQ5RGRs7aiW7M1I/OpF4G7jxWLsw",
                "sha384-IYsDIK5FMbPNV17G3cUJm5KJ3w2tMrXt2z50E0x5YYGBVi8s2x/MLNW9pvVcITLF")
            .SetVersion("4.6.13");

        _manifest
            .DefineStyle("flatpickr")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/css/flatpickr.min.css",
                "~/CrestApps.OrchardCore.Resources/vendors/flatpickr/css/flatpickr.css")
            .SetCdn(
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.min.css",
                "https://cdn.jsdelivr.net/npm/flatpickr@4.6.13/dist/flatpickr.css")
            .SetCdnIntegrity(
                "sha384-RvLlU3fMPPFGDiYrj9DXiCNv6wPcoG++9Ae/3doVdoh/y8GC6Ya+E4F1oOH+m6w+",
                "sha384-pBNX8OFzxVjH58gMO1pgMOJFt3NsWZTJhTLlyet+psDjRpz6jJhIVoKgvyf7KVJM")
            .SetVersion("4.6.13");

        _manifest
            .DefineScript("flatpickr-culture")
            .SetUrl(
                "~/CrestApps.OrchardCore.Resources/scripts/flatpickr-culture.min.js",
                "~/CrestApps.OrchardCore.Resources/scripts/flatpickr-culture.js")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
