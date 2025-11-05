using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Core.Models;
using CrestApps.OrchardCore.AI.Mcp.Core.Services;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.AI.Mcp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Recipes;
using CrestApps.OrchardCore.AI.Mcp.Services;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Mcp;

public sealed class Startup : StartupBase
{
    internal readonly IStringLocalizer S;

    public Startup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDisplayDriver<AIProfile, AIProfileMcpConnectionsDisplayDriver>();
        services.AddScoped<IAICompletionServiceHandler, McpConnectionsAICompletionServiceHandler>();
        services.AddScoped<McpService>();
        services.AddNavigationProvider<McpAdminMenu>();
        services.AddPermissionProvider<McpPermissionsProvider>();
        services.AddScoped<ICatalogEntryHandler<McpConnection>, McpConnectionHandler>();
        services.AddDisplayDriver<McpConnection, McpConnectionDisplayDriver>();
        services.AddScoped<IAICompletionContextBuilderHandler, McpAICompletionContextBuilderHandler>();

        // Register SSE transport type.
        services
            .AddScoped<IMcpClientTransportProvider, SseClientTransportProvider>()
            .AddDisplayDriver<McpConnection, SseMcpConnectionDisplayDriver>()
            .Configure<McpClientAIOptions>(options =>
            {
                options.AddTransportType(McpConstants.TransportTypes.Sse, (entity) =>
                {
                    entity.DisplayName = S["Server-Sent Events"];
                    entity.Description = S["Uses Server-Sent Events over HTTP to receive streaming responses from a remote model server. Great for real-time output from hosted models."];
                });
            });
    }
}

[Feature(McpConstants.Feature.Stdio)]
public sealed class StdIoStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public StdIoStartup(IStringLocalizer<Startup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IMcpClientTransportProvider, StdioClientTransportProvider>()
            .AddDisplayDriver<McpConnection, StdioMcpConnectionDisplayDriver>()
            .Configure<McpClientAIOptions>(options =>
            {
                options.AddTransportType(McpConstants.TransportTypes.StdIo, (entity) =>
                {
                    entity.DisplayName = S["Standard Input/Output"];
                    entity.Description = S["Uses standard input/output streams to communicate with a locally running model process. Ideal for local subprocess integration."];
                });
            });
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<McpConnectionStep>();
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class OCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<McpConnectionDeploymentSource, McpConnectionDeploymentStep, McpConnectionDeploymentStepDisplayDriver>();
    }
}
