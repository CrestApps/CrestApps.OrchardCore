using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddNavigationProvider<AIMcpAdminMenu>();
        services.AddPermissionProvider<AIMcpPermissionsProvider>();
        services.AddScoped<IModelHandler<McpConnection>, McpConnectionHandler>();
        services.AddDisplayDriver<McpConnection, McpConnectionDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.AITools)]
public sealed class ToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileMcpConnectionsDisplayDriver>();
        services.AddScoped<IAICompletionServiceHandler, McpConnectionsAICompletionServiceHandler>();
    }
}
