using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Dispatch.Services;

internal sealed class DispatchResourceConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    public const string BoardScript = "CrestApps.OrchardCore.Dispatch.Board";

    public const string BoardStyle = "CrestApps.OrchardCore.Dispatch.Board";

    private static readonly ResourceManifest _manifest;

    static DispatchResourceConfiguration()
    {
        _manifest = new ResourceManifest();

        _manifest
            .DefineScript(BoardScript)
            .SetUrl(
                "~/CrestApps.OrchardCore.Dispatch/scripts/dispatch-board.min.js",
                "~/CrestApps.OrchardCore.Dispatch/scripts/dispatch-board.js")
            .SetDependencies("crestapps-map")
            .SetVersion("1.0.0");

        _manifest
            .DefineStyle(BoardStyle)
            .SetUrl(
                "~/CrestApps.OrchardCore.Dispatch/styles/dispatch-board.min.css",
                "~/CrestApps.OrchardCore.Dispatch/styles/dispatch-board.css")
            .SetVersion("1.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
