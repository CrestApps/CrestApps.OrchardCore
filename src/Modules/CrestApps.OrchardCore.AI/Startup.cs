using System.Text.Json;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Extensions;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Models;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Handlers;
using CrestApps.OrchardCore.AI.Indexes;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Models;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.Tools;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Services;
using Fluid;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Json;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICoreServices();
        services.AddPermissionProvider<AIPermissionsProvider>();
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
        });

        services
            .AddScoped<IAILinkGenerator, DefaultAILinkGenerator>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAIOptions>, DefaultAIOptionsConfiguration>()
            .AddNavigationProvider<AIProfileAdminMenu>();

        services
            .AddScoped<IAIToolsService, DefaultAIToolsService>()
            .AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>();

        // Add tools core functionality.
        services
            .AddDisplayDriver<AIProfile, AIProfileToolsDisplayDriver>()
            .AddScoped<IAICompletionServiceHandler, FunctionInvocationAICompletionServiceHandler>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<CatalogItemMigrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddDataMigration<AIProfileDefaultContextMigrations>();

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();

        services.AddAITool<ListClientsFunction>(ListClientsFunction.TheName);
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetConnectionsEndpoint();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProfileStep>();
    }
}

[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIResponseMessage>();
        });

        services.AddActivity<AICompletionFromProfileTask, AICompletionFromProfileTaskDisplayDriver>();
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigTaskDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class OCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProfileDeploymentSource, AIProfileDeploymentStep, AIProfileDeploymentStepDisplayDriver>();
        services.AddDeployment<AIDeploymentDeploymentSource, AIDeploymentDeploymentStep, AIDeploymentDeploymentStepDisplayDriver>();
        services.AddDeployment<DeleteAIDeploymentDeploymentSource, DeleteAIDeploymentDeploymentStep, DeleteAIDeploymentDeploymentStepDisplayDriver>();
    }
}

# region Data Sources Feature

[Feature(AIConstants.Feature.DataSources)]
public sealed class DataSourceStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIDataSourceServices();
        services.AddScoped<IAICompletionContextBuilderHandler, DataSourceAICompletionContextBuilderHandler>();
        services.AddDisplayDriver<AIDataSource, AIDataSourceDisplayDriver>();
        services.AddPermissionProvider<AIDataSourcesPermissionProvider>();
        services.AddNavigationProvider<AIDataProviderAdminMenu>();
        services.AddDisplayDriver<AIProfile, AIProfileDataSourceDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.DataSources)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DataSourcesRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDataSourceStep>();
    }
}

[RequireFeatures(AIConstants.Feature.DataSources, "OrchardCore.Deployment")]
public sealed class DataSourcesOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIDataSourceDeploymentSource, AIDataSourceDeploymentStep, AIDataSourceDeploymentStepDisplayDriver>();
    }
}
#endregion

#region Deployments Feature

[Feature(AIConstants.Feature.Deployments)]
public sealed class DeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddAIDeploymentServices()
            .AddPermissionProvider<AIDeploymentPermissionProvider>()
            .AddDisplayDriver<AIDeployment, AIDeploymentDisplayDriver>()
            .AddNavigationProvider<AIDeploymentAdminMenu>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetDeploymentsEndpoint();
    }
}

[Feature(AIConstants.Feature.Deployments)]
[RequireFeatures(AIConstants.Feature.ChatCore)]
public sealed class ChatDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddTransient<ICatalogEntryHandler<AIProfile>, AIDeploymentProfileHandler>()
            .AddDisplayDriver<AIProfile, AIProfileDeploymentDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.Deployments)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class DeploymentRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIDeploymentStep>();
        services.AddRecipeExecutionStep<DeleteAIDeploymentStep>();
    }
}
#endregion

[Feature(AIConstants.Feature.ChatCore)]
public sealed class ChatCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>();
    }
}

[Feature(AIConstants.Feature.ChatApi)]
public sealed class ApiChatStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddApiAIChatSessionEndpoint()
            .AddApiAIUtilityCompletionEndpoint<ApiChatStartup>()
            .AddApiAICompletionEndpoint<ApiChatStartup>();
    }
}

#region Tools Feature

[Feature(AIConstants.Feature.Tools)]
public sealed class ToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IAICompletionContextBuilderHandler, ToolInstancesAICompletionContextBuilderHandler>();
        services.AddDisplayDriver<AIProfile, AIProfileToolInstancesDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, InvokableToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIProfileToolMetadataDisplayDriver>();
        services.AddDisplayDriver<AIToolInstance, AIToolInstanceDisplayDriver>();
        services.AddNavigationProvider<AIToolInstancesAdminMenu>();
        services.AddPermissionProvider<AIToolPermissionProvider>();

        services.AddAIToolSource<ProfileAwareAIToolSource>(ProfileAwareAIToolSource.ToolSource);
        services.AddScoped<IAICompletionServiceHandler, FunctionInstancesAICompletionServiceHandler>();
    }
}

[RequireFeatures(AIConstants.Feature.Tools, "OrchardCore.Recipes.Core")]
public sealed class RecipesToolsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIToolInstanceStep>();
    }
}

[RequireFeatures(AIConstants.Feature.Tools, "OrchardCore.Deployment")]
public sealed class ToolOCDeploymentStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIToolInstanceDeploymentSource, AIToolInstanceDeploymentStep, AIToolInstanceDeploymentStepDisplayDriver>();
    }
}
#endregion

#region Connection Management Feature

[Feature(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AIProviderConnectionHandler>();
        services.AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderConnectionsOptionsConfiguration>();
        services.AddDisplayDriver<AIProviderConnection, AIProviderConnectionDisplayDriver>();
        services.AddNavigationProvider<AIConnectionsAdminMenu>();
        services.AddPermissionProvider<AIConnectionPermissionsProvider>();
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConnectionManagementRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProviderConnectionsStep>();
    }
}

[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConnectionManagementOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProviderConnectionDeploymentSource, AIProviderConnectionDeploymentStep, AIProviderConnectionDeploymentStepDisplayDriver>();
    }
}
#endregion


public sealed class ListClientsFunction : AIFunction
{
    public const string TheName = "ListClients";

    private readonly IHttpContextAccessor _http;
    private readonly IAuthorizationService _authorizationService;
    private readonly DocumentJsonSerializerOptions _options;
    private readonly PagerOptions _pagerOptions;

    public ListClientsFunction(
    IHttpContextAccessor http,
    IAuthorizationService authorizationService,
    IOptions<DocumentJsonSerializerOptions> options,
    IOptions<PagerOptions> pagerOptions)
    {
        _http = http;
        _authorizationService = authorizationService;
        _options = options.Value;
        _pagerOptions = pagerOptions.Value;
    }

    public override string Name => TheName;

    public override string Description => "List clients";

    public override JsonElement JsonSchema => JsonSerializer.Deserialize<JsonElement>(
        """
         {
            "type": "object",
            "properties": {
                "term": {
                    "type": "string", "description": "The query string to search for."
                },
                "pageNumber": {
                    "type": "integer",
                    "description": "The page number of results to return."
                }
            },
            "required": [],
            "additionalProperties": false
         }
         """, JsonSerializerOptions);

    public override JsonElement? ReturnJsonSchema => JsonSerializer.Deserialize<JsonElement>(
        """
        {
          "type": "object",
          "properties": {
            "clients": {
              "type": "array",
              "items": {
                "type": "object",
                "properties": {
                  "Id": { "type": "string" },
                  "Name": { "type": "string" },
                  "CreatedByUserId": { "type": "string" }
                },
                "required": ["Id", "Name", "CreatedByUserId"]
              }
            },
            "pageSize": { "type": "integer" },
            "clientsCount": { "type": "integer" },
            "totalPages": { "type": "integer" },
            "Error": { "type": ["string", "null"], "description": "Contains an error message if something went wrong; null if no error." }
          },
          "required": ["clients", "pageSize", "clientsCount", "totalPages", "Error"],
          "additionalProperties": false
        }
        """, JsonSerializerOptions);

    public override IReadOnlyDictionary<string, object> AdditionalProperties { get; } = new Dictionary<string, object>()
    {
        ["Strict"] = false,
    };

    protected override async ValueTask<object> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        var page = arguments.GetFirstValueOrDefault("pageNumber", 1);

        if (page < 1) page = 1;

        var startingIndex = (page - 1) * _pagerOptions.PageSize;

        IEnumerable<InternalClient> clients = new List<InternalClient>()
        {
            new() { ItemId = "1", Name = "Client A", OwnerId = "User1" },
            new InternalClient { ItemId = "2", Name = "Client B", OwnerId = "User2" },
            new InternalClient { ItemId = "3", Name = "Client C", OwnerId = "User3" },
            new InternalClient { ItemId = "4", Name = "Client D", OwnerId = "User4" },
            new InternalClient { ItemId = "5", Name = "Client E", OwnerId = "User5" },
            new InternalClient { ItemId = "6", Name = "Client F", OwnerId = "User6" },
            new InternalClient { ItemId = "7", Name = "Client G", OwnerId = "User7" },
            new InternalClient { ItemId = "8", Name = "Client H", OwnerId = "User8" },
            new InternalClient { ItemId = "9", Name = "Client I", OwnerId = "User9" },
            new InternalClient { ItemId = "10", Name = "Client J", OwnerId = "User10" },
        };

        var count = clients.Count();

        if (arguments.TryGetFirstString("term", out var term))
        {
            clients = clients.Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        var items = clients.Skip(startingIndex).Take(_pagerOptions.PageSize).Select(x => new
        {
            Id = x.ItemId,
            Name = x.Name,
            CreatedByUserId = x.OwnerId,
        }).ToArray();

        return new
        {
            clients = items,
            pageSize = _pagerOptions.PageSize,
            clientsCount = count,
            totalPages = (int)Math.Ceiling((double)count / _pagerOptions.PageSize),
        };

        /*
        return $$"""
         {
         "clients": {{JsonSerializer.Serialize(items, _options.SerializerOptions)}},
         "pageSize": {{_pagerOptions.PageSize}},
         "clientsCount": {{count}},
         "totalPages": {{Math.Ceiling((double)count / _pagerOptions.PageSize)}}
         }
         """;
        */
    }

    private class InternalClient
    {
        public string ItemId { get; set; }

        public string Name { get; set; }

        public string OwnerId { get; set; }
    }
}
