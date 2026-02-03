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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.Security.Permissions;
using ProtocolRole = ModelContextProtocol.Protocol.Role;

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

[Feature(McpConstants.Feature.Server)]
public sealed class McpServerStartup : StartupBase
{
    private const string McpServerPolicyName = "McpServerPolicy";

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddTransient<IConfigureOptions<McpServerOptions>, McpServerOptionsConfiguration>();
        services.AddPermissionProvider<McpServerPermissionsProvider>();

        // Register the authorization handler for MCP server.
        services.AddScoped<IAuthorizationHandler, McpServerAuthorizationHandler>();

        // Register API key authentication scheme.
        services.AddAuthentication()
            .AddScheme<McpApiKeyAuthenticationOptions, McpApiKeyAuthenticationHandler>(
                McpApiKeyAuthenticationDefaults.AuthenticationScheme, options => { });

        // Register MCP Prompt services.
        services.AddNavigationProvider<McpPromptsAdminMenu>();
        services.AddScoped<ICatalogEntryHandler<McpPrompt>, McpPromptHandler>();
        services.AddDisplayDriver<McpPrompt, McpPromptDisplayDriver>();

        services.AddMcpServer(options =>
        {
            options.ServerInfo = new()
            {
                Name = "Orchard Core MCP Server",
                Version = CrestAppsManifestConstants.Version,
            };
        })
        .WithHttpTransport()
        .WithListToolsHandler((request, cancellationToken) =>
        {
            var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;
            ILogger logger = null;
            var tools = new List<Tool>();

            foreach (var (name, definition) in toolDefinitions.Tools)
            {
                try
                {
                    if (request.Services.GetKeyedService<AITool>(name) is AIFunction aiFunction)
                    {
                        tools.Add(new Tool
                        {
                            Name = aiFunction.Name,
                            Description = aiFunction.Description,
                            InputSchema = aiFunction.JsonSchema,
                        });
                    }
                }
                catch (Exception ex)
                {
                    logger ??= request.Services.GetRequiredService<ILogger<McpServerStartup>>();

                    logger.LogError(ex, "Error creating tool instance for '{ToolName}'", name);
                }
            }

            return ValueTask.FromResult(new ListToolsResult { Tools = tools });
        })
        .WithCallToolHandler(async (request, cancellationToken) =>
        {
            var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;

            if (!toolDefinitions.Tools.ContainsKey(request.Params.Name))
            {
                throw new McpException($"Tool '{request.Params.Name}' not found.");
            }

            if (request.Services.GetKeyedService<AITool>(request.Params.Name) is not AIFunction aiFunction)
            {
                throw new McpException($"Failed to create tool '{request.Params.Name}'.");
            }

            // Convert IDictionary<string, JsonElement> to AIFunctionArguments
            var arguments = new AIFunctionArguments()
            {
                Services = request.Services,
            };

            if (request.Params.Arguments is not null)
            {
                foreach (var kvp in request.Params.Arguments)
                {
                    arguments[kvp.Key] = kvp.Value;
                }
            }

            var result = await aiFunction.InvokeAsync(arguments, cancellationToken);

            return new CallToolResult
            {
                Content = [new TextContentBlock { Text = result?.ToString() ?? string.Empty }]
            };
        })
        .WithListPromptsHandler(async (request, cancellationToken) =>
        {
            var manager = request.Services.GetRequiredService<ICatalogManager<McpPrompt>>();
            var prompts = await manager.GetAllAsync();

            var result = new ListPromptsResult
            {
                Prompts = prompts.Select(p => new Prompt
                {
                    Name = p.Name,
                    Description = p.Description,
                    Arguments = p.Arguments?.Select(a => new PromptArgument
                    {
                        Name = a.Name,
                        Description = a.Description,
                        Required = a.IsRequired,
                    }).ToList(),
                }).ToList()
            };

            return result;
        })
        .WithGetPromptHandler(async (request, cancellationToken) =>
        {
            var manager = request.Services.GetRequiredService<ICatalogManager<McpPrompt>>();
            var prompts = await manager.GetAllAsync();
            var prompt = prompts.FirstOrDefault(p => p.Name == request.Params.Name);

            if (prompt == null)
            {
                throw new McpException($"Prompt '{request.Params.Name}' not found.");
            }

            var messages = new List<PromptMessage>();

            foreach (var msg in prompt.Messages ?? [])
            {
                var content = msg.Content ?? string.Empty;

                // Substitute arguments in the content
                if (request.Params.Arguments is not null)
                {
                    foreach (var arg in request.Params.Arguments)
                    {
                        content = content.Replace($"{{{arg.Key}}}", arg.Value.ToString(), StringComparison.OrdinalIgnoreCase);
                    }
                }

                messages.Add(new PromptMessage
                {
                    Role = msg.Role == McpConstants.Roles.Assistant ? ProtocolRole.Assistant : ProtocolRole.User,
                    Content = new TextContentBlock { Text = content },
                });
            }

            return new GetPromptResult
            {
                Description = prompt.Description,
                Messages = messages,
            };
        });

        // Configure authorization policy.
        // The actual authorization logic is handled by McpServerAuthorizationHandler which reads the options at runtime.
        services.AddAuthorizationBuilder()
            .AddPolicy(McpServerPolicyName, policy =>
            {
                // Add all possible authentication schemes - authentication will be attempted for each.
                // The McpApiKeyAuthenticationHandler returns NoResult if API key auth is not configured,
                // allowing other schemes to be tried.
                policy.AddAuthenticationSchemes(McpApiKeyAuthenticationDefaults.AuthenticationScheme, "Api");
                policy.AddRequirements(new McpServerAuthorizationRequirement());
            });
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        var mcpServerOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var endpoint = routes.MapMcp("mcp");

        // Only require authorization if not using anonymous access.
        if (mcpServerOptions.AuthenticationType != McpServerAuthenticationType.None)
        {
            endpoint.RequireAuthorization(McpServerPolicyName);
        }
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class McpPromptRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<McpPromptStep>();
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class McpPromptDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<McpPromptDeploymentSource, McpPromptDeploymentStep, McpPromptDeploymentStepDisplayDriver>();
    }
}
