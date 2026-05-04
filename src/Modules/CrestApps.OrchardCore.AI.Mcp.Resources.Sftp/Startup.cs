using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Mcp.Sftp;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="Startup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCoreAISftpMcpResources(entry =>
        {
            entry.DisplayName = S["SFTP"];
            entry.Description = S["Reads content from SFTP servers."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = S["The remote file path on the SFTP server."] },
            ];
        });

        services.AddDisplayDriver<McpResource, SftpResourceDisplayDriver>();
        services.AddScoped<IMcpResourceHandler, SftpMcpResourceHandler>();
    }
}
