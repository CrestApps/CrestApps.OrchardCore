using CrestApps.AI.Prompting.Extensions;
using CrestApps.OrchardCore.AI.Core;
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
using CrestApps.OrchardCore.AI.Providers;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.Services;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.BackgroundTasks;
using OrchardCore.Data;
using OrchardCore.Data.Migration;
using OrchardCore.Deployment;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Recipes;
using OrchardCore.ResourceManagement;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI;

public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICoreServices();

        // Register embedded AI templates from this module so they are always
        // available, even when the AI Prompting feature is not enabled.
        services.AddAITemplatesFromAssembly(typeof(Startup).Assembly);
        services.AddPermissionProvider<AIPermissionsProvider>();
        services.Configure<TemplateOptions>(o =>
        {
            o.MemberAccessStrategy.Register<AIProfile>();
            o.MemberAccessStrategy.Register<AIChatSession>();
            o.MemberAccessStrategy.Register<AIChatSessionPrompt>();
            o.MemberAccessStrategy.Register<ExtractedFieldState>();
            o.MemberAccessStrategy.Register<PostSessionResult>();
            o.MemberAccessStrategy.Register<AICompletionReference>();
            o.MemberAccessStrategy.Register<AIToolDefinitionEntry>();
            o.MemberAccessStrategy.Register<ChatDocumentInfo>();
            o.MemberAccessStrategy.Register<ExtractedFieldChange>();
            o.MemberAccessStrategy.Register<ConversionGoalResult>();
        });

        services
            .AddKeyedScoped<IAIReferenceLinkResolver, ContentItemAILinkGenerator>(AIConstants.DataSourceReferenceTypes.Content)
            .AddScoped<CompositeAIReferenceLinkResolver>()
            .AddScoped<CitationReferenceCollector>()
            .AddScoped<PromptTemplateSelectionService>()
            .AddScoped<IAICompletionContextBuilderHandler, AIProfileCompletionContextBuilderHandler>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddTransient<IConfigureOptions<DefaultAIOptions>, DefaultAIOptionsConfiguration>()
            .AddScoped(sp =>
            {
                var defaultOptions = sp.GetRequiredService<IOptionsSnapshot<DefaultAIOptions>>().Value;
                var site = sp.GetRequiredService<ISiteService>().GetSiteSettingsAsync().GetAwaiter().GetResult();

                return defaultOptions.ApplySiteOverrides(site.As<GeneralAISettings>());
            }).AddNavigationProvider<AIProfileAdminMenu>();

        services
              .AddSiteDisplayDriver<GeneralAISettingsDisplayDriver>()
              .AddNavigationProvider<AISiteSettingsAdminMenu>();

        services
            .AddScoped<IAIToolsService, DefaultAIToolsService>()
            .AddTransient<IConfigureOptions<AIProviderOptions>, AIProviderOptionsConfiguration>()
            .AddAIDeploymentServices()
            .AddPermissionProvider<AIDeploymentPermissionProvider>()
            .AddDisplayDriver<AIDeployment, AIDeploymentDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileDeploymentDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDeploymentDisplayDriver>()
            .AddNavigationProvider<AIDeploymentAdminMenu>()
            .AddDataMigration<AIDeploymentTypeMigrations>()
            .AddSiteDisplayDriver<DefaultAIDeploymentSettingsDisplayDriver>()
            .AddTransient<ICatalogEntryHandler<AIProfile>, AIDeploymentProfileHandler>();

        // Add tools core functionality.
        services
            .AddDisplayDriver<AIProfile, AIProfileToolsDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileAgentsDisplayDriver>()
            .AddScoped<IAICompletionServiceHandler, FunctionInvocationAICompletionServiceHandler>()
            .AddPermissionProvider<AIToolPermissionProvider>();

#pragma warning disable CS0618 // Type or member is obsolete
        services.AddDataMigration<CatalogItemMigrations>();
#pragma warning restore CS0618 // Type or member is obsolete

        services.AddDataMigration<AIProfileDefaultContextMigrations>();
        services.AddDataMigration<AIProfileIndexMigrations>();
        services.AddDataMigration<AIProfileDocumentMigrations>();
        services.AddIndexProvider<AIProfileIndexProvider>();

        // AI Profile Template services.
        services
            .AddAIProfileTemplateServices()
            .AddDataMigration<AIProfileTemplateIndexMigrations>()
            .AddIndexProvider<AIProfileTemplateIndexProvider>()
            .AddScoped<IAIProfileTemplateProvider, ModuleAIProfileTemplateProvider>()
            .AddScoped<IAIProfileTemplateProvider, AppDataAIProfileTemplateProvider>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, SystemPromptTemplateDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateToolsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateAgentsDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileTemplateSelectionDisplayDriver>()
            .AddNavigationProvider<AITemplateAdminMenu>()
            .AddPermissionProvider<AIProfileTemplatePermissionsProvider>();

        // Register template sources.
        services
            .AddAITemplateSource(AITemplateSources.Profile, entry =>
            {
                entry.DisplayName = new LocalizedString(AITemplateSources.Profile, "Profile");
                entry.Description = new LocalizedString(AITemplateSources.Profile, "Create a template that can be applied to AI profiles.");
            })
            .AddAITemplateSource(AITemplateSources.SystemPrompt, entry =>
            {
                entry.DisplayName = new LocalizedString(AITemplateSources.SystemPrompt, "System Prompt");
                entry.Description = new LocalizedString(AITemplateSources.SystemPrompt, "Create a reusable system prompt template.");
            });

        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, ResourceManagementOptionsConfiguration>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetDeploymentsEndpoint()
            .AddGetConnectionsEndpoint()
            .AddGetVoicesEndpoint();
    }
}

[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProfileStep>();
        services.AddRecipeExecutionStep<AIProfileTemplateStep>();
        services.AddRecipeExecutionStep<AIDeploymentStep>();
        services.AddRecipeExecutionStep<DeleteAIDeploymentStep>();
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
        services.AddActivity<AIChatSessionFieldExtractedEvent, AIChatSessionFieldExtractedEventDisplayDriver>();
        services.AddActivity<AIChatSessionAllFieldsExtractedEvent, AIChatSessionAllFieldsExtractedEventDisplayDriver>();
        services.AddActivity<AIChatSessionClosedEvent, AIChatSessionClosedEventDisplayDriver>();
        services.AddActivity<AIChatSessionPostProcessedEvent, AIChatSessionPostProcessedEventDisplayDriver>();
    }
}

[RequireFeatures("OrchardCore.Deployment")]
public sealed class OCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProfileDeploymentSource, AIProfileDeploymentStep, AIProfileDeploymentStepDisplayDriver>();
        services.AddDeployment<AIProfileTemplateDeploymentSource, AIProfileTemplateDeploymentStep, AIProfileTemplateDeploymentStepDisplayDriver>();
        services.AddDeployment<AIDeploymentDeploymentSource, AIDeploymentDeploymentStep, AIDeploymentDeploymentStepDisplayDriver>();
        services.AddDeployment<DeleteAIDeploymentDeploymentSource, DeleteAIDeploymentDeploymentStep, DeleteAIDeploymentDeploymentStepDisplayDriver>();
    }
}

[Feature(AIConstants.Feature.ChatCore)]
public sealed class ChatCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>()
            .AddDataMigration<AIChatSessionIndexMigrations>()
            .AddIndexProvider<AIChatSessionIndexProvider>()
            .AddSingleton<IBackgroundTask, AIChatSessionCloseBackgroundTask>();

        services.AddDisplayDriver<AIProfile, AIProfileResponseHandlerDisplayDriver>();

        // Register the AI chat session prompt store.
        services.AddScoped<DefaultAIChatSessionPromptStore>()
            .AddScoped<IAIChatSessionPromptStore>(sp => sp.GetRequiredService<DefaultAIChatSessionPromptStore>())
            .AddIndexProvider<AIChatSessionPromptIndexProvider>()
            .AddDataMigration<AIChatSessionPromptIndexMigrations>()
            .AddDataMigration<AIChatSessionPromptDataMigrations>();

        // Register the data extraction service.
        services.AddScoped<DataExtractionService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, DataExtractionChatSessionHandler>());

        // Register the post-session processing service.
        services.AddScoped<PostSessionProcessingService>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, PostSessionProcessingChatSessionHandler>());

        // Register orchestration services for AI Profile chat
        services.AddOrchestrationServices();
        services.AddDisplayDriver<AIProfileTemplate, ProfileTemplateDisplayDriver>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IOrchestrationContextBuilderHandler, AIToolExecutionContextOrchestrationHandler>());

        // Register the default orchestrator settings UI.
        services.AddSiteDisplayDriver<DefaultOrchestratorSettingsDisplayDriver>();
        services.AddNavigationProvider<AISiteSettingsAdminMenu>();
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

#region Chat Analytics Feature

[Feature(AIConstants.Feature.ChatAnalytics)]
public sealed class ChatAnalyticsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddScoped<AIChatSessionEventService>()
            .AddDataMigration<AIChatSessionMetricsIndexMigrations>()
            .AddIndexProvider<AIChatSessionMetricsIndexProvider>();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, AnalyticsChatSessionHandler>());
    }
}
#endregion
