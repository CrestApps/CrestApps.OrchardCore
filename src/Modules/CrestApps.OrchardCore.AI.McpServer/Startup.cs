using CrestApps.OrchardCore.AI.McpServer.Services;
using CrestApps.OrchardCore.AI.Mcp.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.McpServer;

[Feature(McpConstants.Feature.Server)]
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddPermissionProvider<McpServerPermissionsProvider>();

        services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "Orchard Core MCP Server",
                Version = CrestApps.OrchardCore.CrestAppsManifestConstants.Version,
            };
        })
        .WithTools<OrchardCoreToolsProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapMcp()
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = "Api" });
    }
}
