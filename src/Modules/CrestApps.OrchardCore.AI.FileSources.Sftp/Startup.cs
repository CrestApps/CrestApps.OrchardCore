using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Sftp;
using CrestApps.Core.AI.Sftp.Models;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.FileSources.Sftp.Drivers;
using CrestApps.OrchardCore.AI.FileTransfer.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.FileSources.Sftp;

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

        // The package builds its SFTP client itself, so the server and proxy are checked here before every connection
        // and when a file source is saved through the catalog (review 11).
        services.AddSftpIngestionEgressGuard();

        services.AddDisplayDriver<FileSource, SftpFileSourceDisplayDriver>();
        services.Configure<IngestionSourceSecretOptions>(options => options.Protect<FileSource>(nameof(SftpConnectionMetadata), SftpResourceConstants.DataProtectionPurpose));
    }
}
