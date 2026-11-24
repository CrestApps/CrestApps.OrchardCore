using Microsoft.Extensions.Options;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.AI.WebMcp.Services;

/// <summary>
/// Defines the WebMCP scripts. They come from the <c>@crestapps/webmcp</c> npm package of CrestApps.Core, which the
/// asset build copies into this module.
/// </summary>
internal sealed class ResourceManagementOptionsConfiguration : IConfigureOptions<ResourceManagementOptions>
{
    private const string CdnBaseUrl = "https://cdn.jsdelivr.net/npm/@crestapps/webmcp@2.0.0-preview.258/dist/";

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
                "sha384-0gQxTSXZrKj0ZofQlqa/FSewpyAQmn3/KHMa9BQKWmyveqXe/N74bfNEiipnFEA3",
                "sha384-KPJXGfd8+H2wdrizimNhFKW+nAS82X4wqWw0D9Mifin+xowf/fRa/iZdnkVjGKnj")
            .SetDependencies(WebMcpConstants.ResourceNames.Polyfill)
            .SetVersion("2.0.0");

        _manifest
            .DefineScript(WebMcpConstants.ResourceNames.PageToolsScript)
            .SetUrl("~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.min.js", "~/CrestApps.OrchardCore.AI.WebMcp/scripts/webmcp-page-tools.js")
            .SetCdn(CdnBaseUrl + "webmcp-page-tools.min.js", CdnBaseUrl + "webmcp-page-tools.js")
            .SetCdnIntegrity(
                "sha384-dBVWP+0r9gvrodyKykSDT0owsLuZwywekkpsUghES54LYH04gFfObrkGFS9/iAJO",
                "sha384-wwlti3h/09H+Ywwxs8bhYs6kTs34pwp8QexQVGQ0kKkBo8VnoDiok4iC4FtmoiO0")
            .SetDependencies(WebMcpConstants.ResourceNames.Script)
            .SetVersion("2.0.0");
    }

    public void Configure(ResourceManagementOptions options)
    {
        options.ResourceManifests.Add(_manifest);
    }
}
