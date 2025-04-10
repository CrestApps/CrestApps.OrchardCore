using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Server.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Server.Handlers;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.Mcp.Server;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddMcpServer();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes.MapMcp();
    }
}

[Feature(AIConstants.Feature.AITools)]
public sealed class ToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileMcpServerDisplayDriver>();
        services.AddScoped<IAICompletionServiceHandler, McpServerToolsAICompletionServiceHandler>();
    }
}
