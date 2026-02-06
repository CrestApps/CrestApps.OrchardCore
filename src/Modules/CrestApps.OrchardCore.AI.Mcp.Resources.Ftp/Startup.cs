using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Resources.Ftp.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Resources.Ftp;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<FtpResourceTypeHandler>(FtpResourceConstants.Type, entry =>
        {
            entry.DisplayName = S["FTP/FTPS"];
            entry.Description = S["Reads content from FTP/FTPS servers."];
            entry.UriPatterns = ["{path}"];
        });

        services.AddDisplayDriver<McpResource, FtpResourceDisplayDriver>();
        services.AddScoped<IMcpResourceHandler, FtpMcpResourceHandler>();
    }
}
