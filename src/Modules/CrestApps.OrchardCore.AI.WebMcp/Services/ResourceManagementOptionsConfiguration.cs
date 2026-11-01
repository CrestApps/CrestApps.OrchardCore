using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Defines the WebMCP scripts. They come from the <c>@crestapps/webmcp</c> npm package of CrestApps.Core, which the
/// asset build copies into this module.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private const string CdnBaseUrl = "https://cdn.jsdelivr.net/npm/@crestapps/webmcp@2.0.0-preview.243/dist/";

    private static readonly ResourceManifest _manifest;

    static ResourceManagementOptionsConfiguration()
    {
        _manifest = new ResourceManifest();

        // The W3C Web Machine Learning Community Group's reference polyfill. It installs document.modelContext only in
        // browsers without native WebMCP.
        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.Polyfill)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-polyfill.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-polyfill.js")
            .SetCdn(CdnBaseUrl + "webmcp-polyfill.js", CdnBaseUrl + "webmcp-polyfill.js")
            .SetCdnIntegrity(
                "sha384-aEnX8+GGpZs7j3YINfHJw5npl4H6AkfYHQ5KqdgJzScZ5s76I533BzlPrceDInPx",
                "sha384-aEnX8+GGpZs7j3YINfHJw5npl4H6AkfYHQ5KqdgJzScZ5s76I533BzlPrceDInPx")
            .SetVersion("2.0.0");

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.Script)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp.js")
            .SetCdn(CdnBaseUrl + "webmcp.min.js", CdnBaseUrl + "webmcp.js")
            .SetCdnIntegrity(
                "sha384-g1JnPIbVlyenlllnce9bOaMyciMsq6W9ZENimLTwi3ksT4TO+DMBH0PSGi20D5sC",
                "sha384-4JCt1cqsB4ciVo2DRxLHVe7Z3J1nI1P/ELrXRhRFxXi+xbOOh7ETIu3qdileNJjw")
            .SetDependencies(WebMcpConstants.ResourceNames.Polyfill)
            .SetVersion("2.0.0");

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.PageToolsScript)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.js")
            .SetCdn(CdnBaseUrl + "webmcp-page-tools.min.js", CdnBaseUrl + "webmcp-page-tools.js")
            .SetCdnIntegrity(
                "sha384-+8X8CkGDFmJ4I2ICr4RYH5mbDZ7Ka3faIS802Ee7ZtTYrJQhW/JqG5Nl6yO1Ao2a",
                "sha384-QbIgG9oLi7pNCak0vgzADh+0EFW29ghuAtw9FXMTtd58is0MQfuZlGSkNyyBc8E+")
            .SetDependencies(WebMcpConstants.ResourceNames.Script)
            .SetVersion("2.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
