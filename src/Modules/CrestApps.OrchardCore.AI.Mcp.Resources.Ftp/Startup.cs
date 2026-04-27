using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Ftp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp;

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
        services.AddCoreAIFtpMcpResources(entry =>
        {
            entry.DisplayName = S["FTP/FTPS"];
            entry.Description = S["Reads content from FTP/FTPS servers."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = S["The remote file path on the FTP server."] },
            ];
        });

        services.AddDisplayDriver<McpResource, FtpResourceDisplayDriver>();
        services.AddScoped<IMcpResourceHandler, FtpMcpResourceHandler>();
    }
}
