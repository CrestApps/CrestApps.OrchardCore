using CrestApps.Core.AI.Mcp;
using CrestApps.Core.AI.Mcp.Models;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AgentSkills.Mcp.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Mcp.Core;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Sources;
using CrestApps.OrchardCore.AI.Mcp.Deployments.Steps;
using CrestApps.OrchardCore.AI.Mcp.Drivers;
using CrestApps.OrchardCore.AI.Mcp.Handlers;
using CrestApps.OrchardCore.AI.Mcp.Recipes;
using CrestApps.OrchardCore.AI.Mcp.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
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
        services
            .AddCoreAIMcpClient(includeStdIoTransport: false)
            .AddCoreAIMcpClientStoresYesSql(AIConstants.AICollectionName)
            .AddCoreAISseMcpClientTransport()
            .AddDisplayDriver<AIProfile, AIProfileMcpConnectionsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateMcpConnectionsDisplayDriver>()
            .AddDisplayDriver<ChatInteraction, ChatInteractionMcpConnectionsDisplayDriver>()
            .AddNavigationProvider<McpAdminMenu>()
            .AddPermissionProvider<McpPermissionsProvider>()
            .AddScoped<ICatalogEntryHandler<McpConnection>, McpConnectionHandler>()
            .AddDisplayDriver<McpConnection, McpConnectionDisplayDriver>()
            .AddDisplayDriver<McpConnection, SseMcpConnectionDisplayDriver>();

        services.AddOrchardCoreAgentSkillServices();
    }
}

[Feature(McpPermissions.Feature.Stdio)]
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
            .AddCoreAIStdIoMcpClientTransport()
            .AddDisplayDriver<McpConnection, StdioMcpConnectionDisplayDriver>();
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

[Feature(McpPermissions.Feature.Server)]
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
        services.AddCoreAIMcpServer();

        services.AddOrchardCoreAgentSkillServices();

        // Also register OC implementations under the framework interfaces
        // so the shared WithCrestAppsHandlers() can resolve them.
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
        services.AddCoreAIMcpResourceType<FileResourceTypeHandler>(FileResourceTypeHandler.TypeName, entry =>
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
        .WithCrestAppsHandlers();

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
        // Always apply the authorization policy. The McpServerAuthorizationHandler dynamically
        // checks McpServerOptions.AuthenticationType on every request, allowing the "None" mode
        // to pass through without credentials.
        routes.MapMcp("mcp")
            .RequireAuthorization(McpServerPolicyName);
    }
}

[Feature(McpPermissions.Feature.Server)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class McpPromptRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<McpPromptStep>();
    }
}

[Feature(McpPermissions.Feature.Server)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class McpPromptDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<McpPromptDeploymentSource, McpPromptDeploymentStep, McpPromptDeploymentStepDisplayDriver>();
    }
}

[Feature(McpPermissions.Feature.Server)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class McpResourceRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<McpResourceStep>();
    }
}

[Feature(McpPermissions.Feature.Server)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class McpResourceDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<McpResourceDeploymentSource, McpResourceDeploymentStep, McpResourceDeploymentStepDisplayDriver>();
    }
}

[Feature(McpPermissions.Feature.Server)]
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
        services.AddCoreAIMcpResourceType<ContentByIdResourceTypeHandler>(ContentByIdResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Content Item"];
            entry.Description = S["Retrieves a specific content item by its ID or version ID."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("contentItemId") { Description = S["The content item ID to retrieve."] },
                new McpResourceVariable("contentItemVersionId") { Description = S["The content item version ID to retrieve a specific version."] },
            ];
        });

        services.AddCoreAIMcpResourceType<ContentByTypeResourceTypeHandler>(ContentByTypeResourceTypeHandler.TypeName, entry =>
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

[Feature(McpPermissions.Feature.Server)]
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
        services.AddCoreAIMcpResourceType<RecipeSchemaResourceTypeHandler>(RecipeSchemaResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Recipe Schema"];
            entry.Description = S["Provides the full JSON schema definition for recipes including all steps."];
        });

        services.AddCoreAIMcpResourceType<RecipeStepSchemaResourceTypeHandler>(RecipeStepSchemaResourceTypeHandler.TypeName, entry =>
        {
            entry.DisplayName = S["Recipe Step Schema"];
            entry.Description = S["Provides the JSON schema for a specific recipe step."];
            entry.SupportedVariables =
            [
                new McpResourceVariable("stepName") { Description = S["The name of the recipe step."] },
            ];
        });

        services.AddCoreAIMcpResourceType<RecipeContentResourceTypeHandler>(RecipeContentResourceTypeHandler.TypeName, entry =>
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

[Feature(McpPermissions.Feature.Server)]
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
        services.AddCoreAIMcpResourceType<MediaResourceTypeHandler>(MediaResourceTypeHandler.TypeName, entry =>
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
