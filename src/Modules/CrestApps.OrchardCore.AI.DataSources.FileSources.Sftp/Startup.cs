using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.OrchardCore.AI.DataSources.FileSources.Sftp.Drivers;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.DataSources.FileSources.Sftp;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Registers only the ingestion connector. Exposing SFTP as an MCP resource is a separate feature,
        // and taking this one does not start an MCP server.
        services.AddCoreSftpIngestionConnector();

        services.AddDisplayDriver<WebCrawler, SftpFileSourceDisplayDriver>();
    }
}
