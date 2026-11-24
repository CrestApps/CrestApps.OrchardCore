using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Banking.Checks.Services;

/// <summary>
/// Registers the client-side resources of checks.
/// </summary>
public sealed class ChecksResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    /// <summary>
    /// The name of the script and the stylesheet of the check layout designer.
    /// </summary>
    public const string LayoutDesigner = "check-layout-designer";

    private static readonly ResourceManifest _manifest;

    static ChecksResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(LayoutDesigner)
            .SetUrl("~/CrestApps.OrchardCore.Banking/scripts/check-layout-designer.min.js", "~/CrestApps.OrchardCore.Banking/scripts/check-layout-designer.js")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(LayoutDesigner)
            .SetUrl("~/CrestApps.OrchardCore.Banking/styles/check-layout-designer.min.css", "~/CrestApps.OrchardCore.Banking/styles/check-layout-designer.css")
            .SetVersion("1.0.0");
    }

    /// <inheritdoc/>
    public void Configure(ResourceManagementOptions options)
        => options.ResourceManifests.Add(_manifest);
}
