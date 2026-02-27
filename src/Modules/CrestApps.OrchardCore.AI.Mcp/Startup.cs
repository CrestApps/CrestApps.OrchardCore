using CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;
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
using CrestApps.OrchardCore.AI.Mcp.Tools;
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
using McpServerTool = ModelContextProtocol.Server.McpServerTool;

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
        // Register McpInvokeFunction as a keyed singleton only (not via AddAITool)
        // so it can be resolved by name at runtime but does not appear in the UI tool list.
        services.AddCoreAITool<McpInvokeFunction>(McpInvokeFunction.FunctionName);

        services.AddDisplayDriver<AIProfile, AIProfileMcpConnectionsDisplayDriver>();
        services.AddDisplayDriver<ChatInteraction, ChatInteractionMcpConnectionsDisplayDriver>();
        services.AddScoped<McpService>();
        services.AddScoped<IMcpServerMetadataCacheProvider, DefaultMcpServerMetadataProvider>();
        services.AddSingleton<IMcpMetadataPromptGenerator, DefaultMcpMetadataPromptGenerator>();
        services.AddSingleton<IMcpCapabilityEmbeddingCacheProvider, InMemoryMcpCapabilityEmbeddingCacheProvider>();
        services.AddScoped<IMcpCapabilityResolver, DefaultMcpCapabilityResolver>();
        services.AddOptions<McpCapabilityResolverOptions>();
        services.AddScoped<IToolRegistryProvider, McpToolRegistryProvider>();
        services.AddNavigationProvider<McpAdminMenu>();
        services.AddPermissionProvider<McpPermissionsProvider>();
        services.AddScoped<ICatalogEntryHandler<McpConnection>, McpConnectionHandler>();
        services.AddDisplayDriver<McpConnection, McpConnectionDisplayDriver>();
        services.AddScoped<IAICompletionContextBuilderHandler, McpAICompletionContextBuilderHandler>();

        services.AddOrchardCoreAgentSkillServices();

        services.AddOptions<McpMetadataCacheOptions>();

        // Register SSE transport type.
        services
            .AddScoped<IMcpClientTransportProvider, SseClientTransportProvider>()
            .AddScoped<IOAuth2TokenService, DefaultOAuth2TokenService>()
            .AddScoped<ICatalogEntryHandler<McpConnection>, SseMcpConnectionSettingsHandler>()
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

    internal readonly IStringLocalizer S;

    public McpServerStartup(IStringLocalizer<McpServerStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddOrchardCoreAgentSkillServices();
        services.AddScoped<IMcpServerPromptService, DefaultMcpServerPromptService>();
        services.AddScoped<IMcpServerResourceService, DefaultMcpServerResourceService>();
        services.AddTransient<IConfigureOptions<McpServerOptions>, McpServerOptionsConfiguration>();
        services.AddPermissionProvider<McpServerPermissionsProvider>();

        // Register the authorization handler for MCP server.
        services.AddScoped<IAuthorizationHandler, McpServerAuthorizationHandler>();

        // Register API key authentication scheme.
        services.AddAuthentication()
            .AddScheme<McpApiKeyAuthenticationOptions, McpApiKeyAuthenticationHandler>(
                McpApiKeyAuthenticationDefaults.AuthenticationScheme, options => { });

        // Register MCP Prompt services.
        services.AddNavigationProvider<McpPromptsAdminMenu>()
            .AddScoped<ICatalogEntryHandler<McpPrompt>, McpPromptHandler>()
            .AddDisplayDriver<McpPrompt, McpPromptDisplayDriver>();

        // Register MCP Resource services.
        services.AddNavigationProvider<McpResourcesAdminMenu>()
            .AddScoped<ICatalogEntryHandler<McpResource>, McpResourceHandler>()
            .AddDisplayDriver<McpResource, McpResourceDisplayDriver>();

        // Register the file provider resolver.
        services.AddScoped<IMcpFileProviderResolver, DefaultMcpFileProviderResolver>();

        // Register built-in File resource type handler.
        services.AddMcpResourceType<FileResourceTypeHandler>(FileResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["File"];
            entry.Description = S["Reads content from file providers."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("providerName") { Description = S["The name of the file provider to use."] },
                new McpResourceVariable("fileName") { Description = S["The file path within the provider."] },
            ];
        });

        _ = services.AddMcpServer(options =>
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

            // Include tools registered via the MCP C# SDK (e.g., via [McpServerToolType] attribute).
            var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();

            if (sdkTools is not null)
            {
                foreach (var sdkTool in sdkTools)
                {
                    if (!tools.Any(t => t.Name == sdkTool.ProtocolTool.Name))
                    {
                        tools.Add(sdkTool.ProtocolTool);
                    }
                }
            }

            return ValueTask.FromResult(new ListToolsResult { Tools = tools });
        })
        .WithCallToolHandler(async (request, cancellationToken) =>
        {
            var toolDefinitions = request.Services.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value;

            if (toolDefinitions.Tools.ContainsKey(request.Params.Name))
            {
                if (request.Services.GetKeyedService<AITool>(request.Params.Name) is not AIFunction aiFunction)
                {
                    throw new McpException($"Failed to create tool '{request.Params.Name}'.");
                }

                // Convert IDictionary<string, JsonElement> to AIFunctionArguments
                var arguments = new AIFunctionArguments()
                {
                    Services = request.Services,
                    Context = new Dictionary<object, object>()
                    {
                        ["mcpRequest"] = request,
                    },
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
            }

            // Try tools registered via the MCP C# SDK (e.g., via [McpServerToolType] attribute).
            var sdkTools = request.Services.GetService<IEnumerable<McpServerTool>>();
            var sdkTool = sdkTools?.FirstOrDefault(t => t.ProtocolTool.Name == request.Params.Name);

            if (sdkTool is not null)
            {
                return await sdkTool.InvokeAsync(request, cancellationToken);
            }

            throw new McpException($"Tool '{request.Params.Name}' not found.");
        })
        .WithListPromptsHandler(async (request, cancellationToken) =>
        {
            var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

            return new ListPromptsResult
            {
                Prompts = await promptService.ListAsync(),
            };
        })
        .WithGetPromptHandler(async (request, cancellationToken) =>
        {
            var promptService = request.Services.GetRequiredService<IMcpServerPromptService>();

            return await promptService.GetAsync(request, cancellationToken);
        })
        .WithListResourcesHandler(async (request, cancellationToken) =>
        {
            var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

            return new ListResourcesResult
            {
                Resources = await resourceService.ListAsync(),
            };
        })
        .WithListResourceTemplatesHandler(async (request, cancellationToken) =>
        {
            var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

            return new ListResourceTemplatesResult
            {
                ResourceTemplates = await resourceService.ListTemplatesAsync(),
            };
        })
        .WithReadResourceHandler(async (request, cancellationToken) =>
        {
            var resourceService = request.Services.GetRequiredService<IMcpServerResourceService>();

            return await resourceService.ReadAsync(request, cancellationToken);
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

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class McpResourceRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<McpResourceStep>();
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class McpResourceDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<McpResourceDeploymentSource, McpResourceDeploymentStep, McpResourceDeploymentStepDisplayDriver>();
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.ContentManagement")]
public sealed class McpContentResourceStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public McpContentResourceStartup(IStringLocalizer<McpContentResourceStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<ContentByIdResourceTypeHandler>(ContentByIdResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Content Item"];
            entry.Description = S["Retrieves a specific content item by its ID or version ID."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("contentItemId") { Description = S["The content item ID to retrieve."] },
                new McpResourceVariable("contentItemVersionId") { Description = S["The content item version ID to retrieve a specific version."] },
            ];
        });

        services.AddMcpResourceType<ContentByTypeResourceTypeHandler>(ContentByTypeResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Content Type"];
            entry.Description = S["Lists all published content items of a given content type."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("contentType") { Description = S["The content type to query."] },
            ];
        });
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("CrestApps.OrchardCore.Recipes")]
public sealed class McpRecipeSchemaResourceStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public McpRecipeSchemaResourceStartup(IStringLocalizer<McpRecipeSchemaResourceStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<RecipeSchemaResourceTypeHandler>(RecipeSchemaResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Recipe Schema"];
            entry.Description = S["Provides the full JSON schema definition for recipes including all steps."];
        });

        services.AddMcpResourceType<RecipeStepSchemaResourceTypeHandler>(RecipeStepSchemaResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Recipe Step Schema"];
            entry.Description = S["Provides the JSON schema for a specific recipe step."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("stepName") { Description = S["The name of the recipe step."] },
            ];
        });

        services.AddMcpResourceType<RecipeContentResourceTypeHandler>(RecipeContentResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Recipe"];
            entry.Description = S["Returns the JSON content of a specific recipe by name."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("recipeName") { Description = S["The name of the recipe to retrieve."] },
            ];
        });
    }
}

[Feature(McpConstants.Feature.Server)]
[RequireFeatures("OrchardCore.Media")]
public sealed class McpMediaResourceStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    public McpMediaResourceStartup(IStringLocalizer<McpMediaResourceStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddMcpResourceType<MediaResourceTypeHandler>(MediaResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Media"];
            entry.Description = S["Reads files from Orchard Core's media store."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("path") { Description = S["The media path to read."] },
            ];
        });
    }
}
