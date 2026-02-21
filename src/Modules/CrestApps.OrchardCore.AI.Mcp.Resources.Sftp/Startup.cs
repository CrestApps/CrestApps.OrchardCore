using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Sftp.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Sftp;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<SftpResourceTypeHandler>(SftpResourceConstants.Type, entry =>
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
