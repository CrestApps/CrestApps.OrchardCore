using CrestApps.Core.AI.Ftp;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.FileSources.Ftp.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.FileSources.Ftp;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers only the ingestion connector. Exposing FTP as an MCP resource is a separate feature,
        // and taking this one does not start an MCP server.
        services.AddCoreFtpIngestionConnector();

        services.AddDisplayDriver<WebCrawler, FtpFileSourceDisplayDriver>();
    }
}
