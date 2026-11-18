using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AdminTheme;

internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // Restyles TheAdmin, so it is always rendered after TheAdmin's stylesheets.
        _manifest
            .DefineStyle("crestapps-admin-theme")
            .SetUrl(
                "~/CrestApps.OrchardCore.AdminTheme/styles/admin-theme.min.css",
                "~/CrestApps.OrchardCore.AdminTheme/styles/admin-theme.css")
            .SetDependencies("the-admin")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("crestapps-admin-theme-scheme")
            .SetUrl(
                "~/CrestApps.OrchardCore.AdminTheme/scripts/color-scheme.min.js",
                "~/CrestApps.OrchardCore.AdminTheme/scripts/color-scheme.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineScript("crestapps-admin-theme-scheme-editor")
            .SetUrl(
                "~/CrestApps.OrchardCore.AdminTheme/scripts/color-scheme-editor.min.js",
                "~/CrestApps.OrchardCore.AdminTheme/scripts/color-scheme-editor.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle("crestapps-admin-theme-login")
            .SetUrl(
                "~/CrestApps.OrchardCore.AdminTheme/styles/login.min.css",
                "~/CrestApps.OrchardCore.AdminTheme/styles/login.css")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
