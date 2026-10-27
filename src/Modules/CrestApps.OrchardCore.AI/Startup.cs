using CrestApps.Core;
using CrestApps.Core.AI;
using CrestApps.Core.AI.Chat;
using CrestApps.Core.AI.Chat.Services;
using CrestApps.Core.AI.Completions;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.Orchestration;
using CrestApps.Core.AI.Profiles;
using CrestApps.Core.AI.Services;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.AI.Tooling.Instances;
using CrestApps.Core.AI.Tooling.Instances.Documentation;
using CrestApps.Core.AI.Tooling.Parameters;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.Core.Data.YesSql.Services;
using CrestApps.Core.Services;
using CrestApps.Core.Templates.Extensions;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Core.Conversations;
using CrestApps.OrchardCore.AI.Core.Handlers;
using CrestApps.OrchardCore.AI.Core.Indexes;
using CrestApps.OrchardCore.AI.Core.Services;
using CrestApps.OrchardCore.AI.Deployments.Drivers;
using CrestApps.OrchardCore.AI.Deployments.Sources;
using CrestApps.OrchardCore.AI.Deployments.Steps;
using CrestApps.OrchardCore.AI.Drivers;
using CrestApps.OrchardCore.AI.Endpoints;
using CrestApps.OrchardCore.AI.Endpoints.Api;
using CrestApps.OrchardCore.AI.Handlers;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.AI.Prompting;
using CrestApps.OrchardCore.AI.Providers;
using CrestApps.OrchardCore.AI.Recipes;
using CrestApps.OrchardCore.AI.Services;
using CrestApps.OrchardCore.AI.Tools.Drivers;
using CrestApps.OrchardCore.AI.Tools.Handlers;
using CrestApps.OrchardCore.AI.Tools.Services;
using CrestApps.OrchardCore.AI.ViewModels;
using CrestApps.OrchardCore.AI.Workflows.Drivers;
using CrestApps.OrchardCore.AI.Workflows.Models;
using CrestApps.OrchardCore.AI.Workflows.Services;
using CrestApps.OrchardCore.AI.Workflows.Tools;
using CrestApps.OrchardCore.Core;
using CrestApps.OrchardCore.Core.Services;
using Fluid;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
using OrchardCore.Scripting;
using OrchardCore.Security.Permissions;
using OrchardCore.Workflows.Helpers;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers services and configuration for this feature.
/// </summary>
public sealed class Startup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAICoreServices()
            .AddCoreAIServicesStoresYesSql()
            // Has to follow the YesSql stores: they bind the connection and deployment catalogs to the
            // database alone, which hides configuration-backed entries from everything that resolves a
            // catalog instead of a store.
            .AddMultiSourceAICatalogs()
            .AddDataMigration<AIDeploymentIndexMigrations>()
            .AddDataMigration<AIProfileIndexMigrations>()
            .AddDataMigration<AIProviderConnectionIndexMigrations>()
            .AddCatalogManagers()
            .TryAddScoped<IStoreCommitter, YesSqlStoreCommitter>();

        services.AddSingleton<TimeProvider, ClockTimeProviderAdapter>();

        // Register embedded templates from this module so they are always
        // available, even when the Templating feature is not enabled.
        services.AddTemplatesFromAssembly(typeof(Startup).Assembly);
        services.AddPermissionProvider<AIPermissionsProvider>();

        services
            .AddCatalogs()
            .AddKeyedScoped<IAIReferenceLinkResolver, ContentItemAILinkGenerator>(AIConstants.DataSourceReferenceTypes.Content)
            .AddScoped<CompositeAIReferenceLinkResolver>()
            .AddScoped<Core.Services.CitationReferenceCollector>()
            .AddScoped<PromptTemplateSelectionService>()
            .AddScoped<IAIToolInstanceAccessor, DefaultAIToolInstanceAccessor>()
            .AddDisplayDriver<AIProfile, AIProfileDisplayDriver>()
            .AddTransient<IConfigureOptions<GeneralAIOptions>, GeneralAIOptionsConfiguration>()
            .AddSignalOptionsChangeTokenSource<GeneralAIOptions>()
            .AddTransient<IConfigureOptions<DefaultAIOptions>, DefaultAIOptionsConfiguration>()
            .AddNavigationProvider<AIProfileAdminMenu>();

        services
            .AddSiteDisplayDriver<GeneralAISettingsDisplayDriver>()
            .AddNavigationProvider<AISiteSettingsAdminMenu>();
        services.AddSiteSettingsPermission(AIConstants.AISettingsGroupId, AIPermissions.ManageAIProfiles);

        services
            .AddAIDeploymentServices()
            .AddPermissionProvider<AIDeploymentPermissionProvider>()
            .AddDisplayDriver<AIDeployment, AIDeploymentDisplayDriver>()
            .AddDisplayDriver<AIDeployment, AIDeploymentModelCapabilitiesDisplayDriver>()
            .AddDisplayDriver<AIDeployment, AIDeploymentCascadedRealtimeDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileDeploymentDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileModelParametersDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileUtilityModelParametersDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDeploymentDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateModelParametersDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateUtilityModelParametersDisplayDriver>()
            .AddNavigationProvider<AIDeploymentAdminMenu>()
            .AddDataMigration<AIDeploymentTypeMigrations>()
            .AddDataMigration<AIDeploymentV1DocumentMigrations>()
            .AddSiteDisplayDriver<DefaultAIDeploymentSettingsDisplayDriver>();

        // Add tools core functionality.
        services
            .AddDisplayDriver<AIProfile, AIProfileToolsDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileAgentsDisplayDriver>()
            .AddScoped<IAIToolAccessEvaluator, OrchardCoreAIToolAccessEvaluator>()
            .AddPermissionProvider<AIToolPermissionProvider>();

        // Conversations: the system message rendered with the conversation's values, and tool parameters filled
        // from the conversation, on every channel.
        services.AddScoped<IAICompletionContextBuilderHandler, AIConversationLiquidCompletionContextBuilderHandler>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAIToolParameterContextResolver, AIConversationToolParameterContextResolver>());
        services.TryAddScoped<IAIConversationStartingService, DefaultAIConversationStartingService>();
        services.TryAddScoped<IAIConversationTurnNotifier, AIConversationTurnNotifier>();

        // Quality: scoring conversations, simulated test conversations and the metrics alerts watch.
        services
            .AddScoped<IAIConversationEvaluator, AIConversationEvaluator>()
            .AddScoped<IAIConversationEvaluationStore, YesSqlAIConversationEvaluationStore>()
            .AddScoped<AIConversationEvaluationService>()
            .AddScoped<AIConversationSimulator>()
            .AddScoped<AIConversationMetrics>()
            .AddDataMigration<AIConversationEvaluationIndexMigrations>()
            .AddIndexProvider<AIConversationEvaluationIndexProvider>();

        // Contact memory: a note per contact the profile's instructions get and its conversations rewrite.
        services
            .AddScoped<IAIContactMemoryStore, YesSqlAIContactMemoryStore>()
            .AddScoped<AIContactMemoryService>()
            .AddScoped<IAICompletionContextBuilderHandler, AIContactMemoryCompletionContextBuilderHandler>()
            .AddDataMigration<AIContactMemoryIndexMigrations>()
            .AddIndexProvider<AIContactMemoryIndexProvider>();

        // Speaking styles (tested prompt sections a profile turns on) and guardrails (topics a profile refuses).
        services
            .AddScoped<IAICompletionContextBuilderHandler, AISpeakingStyleCompletionContextBuilderHandler>()
            .AddDisplayDriver<AIProfile, AIProfileSpeakingStyleDisplayDriver>()
            .AddScoped<AIGuardrailService>();

        // Profile versions: every save of a profile is kept, numbered, and can be compared and restored.
        services
            .AddScoped<IAIProfileVersionStore, YesSqlAIProfileVersionStore>()
            .AddScoped<ICatalogEntryHandler<AIProfile>, AIProfileVersionHandler>()
            .AddDisplayDriver<AIProfileVersionEntry, AIProfileVersionEntryDisplayDriver>()
            .AddDataMigration<AIProfileVersionIndexMigrations>()
            .AddIndexProvider<AIProfileVersionIndexProvider>();
        services.AddScoped<IOrchestrationContextBuilderHandler, ChatConversationStartingOrchestrationHandler>();
        services.Configure<TemplateOptions>(options =>
        {
            options.MemberAccessStrategy.Register<AIConversationTemplateModel>();
            options.MemberAccessStrategy.Register<AIConversationTurn>();
            options.MemberAccessStrategy.Register<AIConversationEvaluation>();
            options.MemberAccessStrategy.Register<AIConversationCriterionResult>();
            options.MemberAccessStrategy.Register<AIConversationIssue>();
            options.MemberAccessStrategy.Register<AIChatSession>();
            options.MemberAccessStrategy.Register<AIProfile>();
        });

        services.AddDataMigration<AIProfileDefaultContextMigrations>();
        services.AddDataMigration<AIProfileDocumentMigrations>();
        services.AddDataMigration<AILegacyDocumentTypeNameMigrations>();
        services.AddDataMigration<AIProfileV1DocumentMigrations>();

        // AI Profile Template services.
        services
            .AddCoreAIProfileTemplateStoresYesSql()
            .AddDataMigration<AIProfileTemplateIndexMigrations>()
            .AddScoped<DefaultAIProfileTemplateManager>()
            .AddScoped<IAIProfileTemplateManager>(sp => sp.GetRequiredService<DefaultAIProfileTemplateManager>())
            .AddScoped<INamedSourceCatalogManager<AIProfileTemplate>>(sp => sp.GetRequiredService<DefaultAIProfileTemplateManager>())
            .AddScoped<ICatalogEntryHandler<AIProfileTemplate>, AIProfileTemplateHandler>()
            .AddScoped<IAIProfileTemplateProvider, ModuleAIProfileTemplateProvider>()
            .AddScoped<IAIProfileTemplateProvider, AppDataAIProfileTemplateProvider>()
            .AddScoped<AIProfileTemplateProfileFactory>()
            .AddScoped<ProfileScenarioCatalog>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, SystemPromptTemplateDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateToolsDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateAgentsDisplayDriver>()
            .AddNavigationProvider<AITemplateAdminMenu>()
            .AddPermissionProvider<AIProfileTemplatePermissionsProvider>();
    }

    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddGetDeploymentsEndpoint()
            .AddGetConnectionsEndpoint()
            .AddGetVoicesEndpoint();
    }
}

/// <summary>
/// Registers services and configuration for the Indexing feature.
/// </summary>
/// <summary>
/// Registers the deployment provider for cascaded realtime deployments: deployments that answer speech with
/// speech by chaining a speech-to-text, a chat, and a text-to-speech deployment rather than talking to a
/// provider of their own.
/// </summary>
public sealed class CascadedRealtimeStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="CascadedRealtimeStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public CascadedRealtimeStartup(IStringLocalizer<CascadedRealtimeStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override void ConfigureServices(IServiceCollection services)
    {
        // The deployment carries no connection of its own; it only names the deployments to chain.
        services.AddCoreAIDeploymentProvider(AIConstants.CascadedRealtimeClientName, options =>
        {
            options.DisplayName = S["Cascaded Realtime"];
            options.Description = S["Answers speech with speech by chaining a speech-to-text, a chat, and a text-to-speech deployment."];
            options.UseContainedConnection = true;
        });
    }
}

[RequireFeatures("OrchardCore.Indexing")]
public sealed class IndexingStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDataMigration<IndexProfileEmbeddingMetadataMigrations>();
    }
}

/// <summary>
/// Registers services and configuration for the Recipes feature.
/// </summary>
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class RecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IGlobalMethodProvider, AITemplateMethodProvider>();
        services.AddRecipeExecutionStep<AIProfileStep>();
        services.AddRecipeExecutionStep<CreateAIProfileFromTemplateStep>();
        services.AddRecipeExecutionStep<AIProfileTemplateStep>();
        services.AddRecipeExecutionStep<AIDeploymentStep>();
        services.AddRecipeExecutionStep<DeleteAIDeploymentStep>();
    }
}

/// <summary>
/// Registers services and configuration for the Workflows feature.
/// </summary>
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AICompletionFromProfileTask, AICompletionFromProfileTaskDisplayDriver>();
        services.AddActivity<AIChatSessionFieldExtractedEvent, AIChatSessionFieldExtractedEventDisplayDriver>();
        services.AddActivity<AIChatSessionAllFieldsExtractedEvent, AIChatSessionAllFieldsExtractedEventDisplayDriver>();
        services.AddActivity<AIChatSessionClosedEvent, AIChatSessionClosedEventDisplayDriver>();
        services.AddActivity<AIChatSessionPostProcessedEvent, AIChatSessionPostProcessedEventDisplayDriver>();

        // AI conversations: workflows that run before a conversation starts, and the tasks they decide with.
        services.AddActivity<AIConversationStartingEvent, AIConversationStartingEventDisplayDriver>();
        services.AddActivity<SetConversationVariablesTask, SetConversationVariablesTaskDisplayDriver>();
        services.AddActivity<UseAIProfileTask, UseAIProfileTaskDisplayDriver>();
        services.AddActivity<DeclineConversationTask, DeclineConversationTaskDisplayDriver>();
        services.AddActivity<AIToolCalledEvent, AIToolCalledEventDisplayDriver>();
        services.AddScoped<IAIConversationStartingHandler, WorkflowAIConversationStartingHandler>();
        services.AddActivity<AIConversationTurnEvent, AIConversationTurnEventDisplayDriver>();
        services.AddScoped<IAIConversationTurnHandler, WorkflowAIConversationTurnHandler>();

        // Quality: A/B splits, QA evaluations, simulated tests, analysis reruns and metric alerts.
        services.AddActivity<SplitTrafficTask, SplitTrafficTaskDisplayDriver>();
        services.AddActivity<EvaluateConversationTask, EvaluateConversationTaskDisplayDriver>();
        services.AddActivity<AIConversationEvaluatedEvent, AIConversationEvaluatedEventDisplayDriver>();
        services.AddActivity<SimulateConversationTask, SimulateConversationTaskDisplayDriver>();
        services.AddActivity<RerunConversationAnalysisTask, RerunConversationAnalysisTaskDisplayDriver>();
        services.AddActivity<CheckConversationMetricTask, CheckConversationMetricTaskDisplayDriver>();

        // Memory and integrations: rewriting a contact's memory, and signed webhooks to other systems.
        services.AddActivity<UpdateContactMemoryTask, UpdateContactMemoryTaskDisplayDriver>();
        services.AddActivity<SendSignedWebhookTask, SendSignedWebhookTaskDisplayDriver>();
        services.AddActivity<AIGuardrailTriggeredEvent, AIGuardrailTriggeredEventDisplayDriver>();
        services.AddScoped<IAIGuardrailHandler, WorkflowAIGuardrailHandler>();
        services.AddHttpClient(SendSignedWebhookTask.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(15));
    }
}

/// <summary>
/// Registers the <c>Workflow</c> tool instance source: a tool the AI calls that runs a workflow and answers with the
/// workflow's output.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
[RequireFeatures("OrchardCore.Workflows")]
public sealed class WorkflowToolInstancesStartup : StartupBase
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowToolInstancesStartup"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public WorkflowToolInstancesStartup(IStringLocalizer<WorkflowToolInstancesStartup> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddAIToolInstanceSource<WorkflowToolInstanceSource>(WorkflowToolConstants.SourceName, entry =>
        {
            entry.DisplayName = S["Workflow"];
            entry.Description = S["Runs a workflow that starts with the AI Tool Called event, and answers with the workflow's output."];
            entry.Category = S["Automation"];
            entry.Parameters = new AIToolInstanceParameterCapabilities
            {
                Bindings =
                [
                    new AIToolParameterBindingOption(WorkflowToolConstants.ArgumentBinding)
                    {
                        DisplayName = S["Workflow argument"],
                        Hint = S["Passed to the workflow as Workflow.Input.Arguments, under the parameter's name."],
                        SupportsTargetName = false,
                    },
                ],
                Hint = S["Declare what the workflow needs. The model fills each one, or you fix it, or it comes from the conversation, such as the customer's number or a conversation variable."],
            };
        });

        services.AddDisplayDriver<AIToolInstance, WorkflowToolInstanceDisplayDriver>();
    }
}

/// <summary>
/// Registers the AI completion with config workflow activity and its feature-contributed display drivers.
/// The activity relies on the Chat Interactions completion context builder to translate its configuration
/// into an AI completion, so it is only available when that feature is enabled.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Interactions", "OrchardCore.Workflows")]
public sealed class ChatInteractionsWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigTaskDisplayDriver>();
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigConnectionDisplayDriver>();
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigParametersDisplayDriver>();
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigToolsDisplayDriver>();
    }
}

/// <summary>
/// Contributes the prompt template selection field to the AI completion with config workflow activity.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Interactions", AIPromptingConstants.Feature.Area, "OrchardCore.Workflows")]
public sealed class ChatInteractionsPromptingWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigPromptSelectionDisplayDriver>();
    }
}

/// <summary>
/// Contributes the AI tool instances field to the AI completion with config workflow activity.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Interactions", AIConstants.Feature.ToolInstances, "OrchardCore.Workflows")]
public sealed class ChatInteractionsToolInstancesWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigToolInstancesDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the OCDeployments feature.
/// </summary>
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

/// <summary>
/// Registers services and configuration for the ChatCore feature.
/// </summary>
[Feature(AIConstants.Feature.ChatCore)]
public sealed class ChatCoreStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddCoreAIChatSessionStoresYesSql()
            .AddScoped<IAIChatSessionManager, DefaultAIChatSessionManager>()
            .AddDataMigration<AIChatSessionIndexMigrations>();

        // Closing a session, after inactivity or as soon as its call ends.
        services.AddScoped<AIChatSessionCloser>();
        services.AddScoped<IAIChatSessionCloser>(sp => sp.GetRequiredService<AIChatSessionCloser>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AIChatSessionCloseBackgroundTask>());

        // Privacy: redacting or stripping ended conversations, and deleting those past the profile's retention.
        services.AddScoped<AIConversationPrivacyService>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IBackgroundTask, AIConversationPrivacyBackgroundTask>());

        services.AddDisplayDriver<AIProfile, AIProfileResponseHandlerDisplayDriver>();

        // Register the AI chat session prompt store.
        services
            .AddDataMigration<AIChatSessionPromptIndexMigrations>()
            .AddDataMigration<AIChatSessionPromptDataMigrations>()
            .AddDataMigration<AIChatSessionV1PromptDataMigrations>();

        services.AddCoreAIChatSessionProcessing();

        // OC uses IBackgroundTask with distributed locking instead of IHostedService,
        // so remove the framework's hosted service and runner.
        services.RemoveAll<AIChatSessionCloseRunner>();

        var hostedServiceDescriptor = services.FirstOrDefault(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationType == typeof(AIChatSessionCloseBackgroundService));

        if (hostedServiceDescriptor is not null)
        {
            services.Remove(hostedServiceDescriptor);
        }

        // Register orchestration services for AI Profile chat
        services.AddDisplayDriver<AIProfileTemplate, ProfileTemplateDisplayDriver>();

        // Register the default orchestrator settings UI.
        services.AddSiteDisplayDriver<DefaultOrchestratorSettingsDisplayDriver>();
        services.AddSiteSettingsPermission(AIConstants.AISettingsGroupId, AIPermissions.ManageAIProfiles);
        services.AddNavigationProvider<AISiteSettingsAdminMenu>();

        services.AddResourceConfiguration<ResourceManagementOptionsConfiguration>();
    }
}

/// <summary>
/// Registers services and configuration for the ChatWorkflows feature.
/// </summary>
[RequireFeatures(AIConstants.Feature.ChatCore, "OrchardCore.Workflows")]
public sealed class ChatWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, DataExtractionChatSessionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, ConversationTurnChatSessionHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IAIChatSessionHandler, PostSessionProcessingChatSessionHandler>());
    }
}

/// <summary>
/// Registers services and configuration for the ApiChat feature.
/// </summary>
[Feature(AIConstants.Feature.ChatApi)]
public sealed class ApiChatStartup : StartupBase
{
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
    {
        routes
            .AddApiAIChatSessionEndpoint()
            .AddApiAIUtilityCompletionEndpoint<ApiChatStartup>()
            .AddApiAICompletionEndpoint<ApiChatStartup>()
            .MapAIConversationsApi();
    }
}

/// <summary>
/// Registers services and configuration for the ConnectionManagement feature.
/// </summary>
[Feature(AIConstants.Feature.ConnectionManagement)]
public sealed class ConnectionManagementStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ICatalogEntryHandler<AIProviderConnection>, AIProviderConnectionHandler>();

        services.AddDisplayDriver<AIProviderConnection, AIProviderConnectionDisplayDriver>();
        services.AddNavigationProvider<AIConnectionsAdminMenu>();
        services.AddPermissionProvider<AIConnectionPermissionsProvider>();
    }
}

/// <summary>
/// Registers services and configuration for the ConnectionManagementRecipes feature.
/// </summary>
[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ConnectionManagementRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIProviderConnectionsStep>();
    }
}

/// <summary>
/// Registers services and configuration for the ConnectionManagementOCDeployments feature.
/// </summary>
[Feature(AIConstants.Feature.ConnectionManagement)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ConnectionManagementOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIProviderConnectionDeploymentSource, AIProviderConnectionDeploymentStep, AIProviderConnectionDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the ChatAnalytics feature.
/// </summary>
[Feature(AIConstants.Feature.ChatAnalytics)]
public sealed class ChatAnalyticsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddDataMigration<AIChatSessionMetricsIndexMigrations>()
            .AddDataMigration<AICompletionUsageIndexMigrations>()
            .AddIndexProvider<AICompletionUsageIndexProvider>()
            .AddDataMigration<AIVoiceSessionSummaryIndexMigrations>()
            .AddIndexProvider<AIVoiceSessionSummaryIndexProvider>();

        // The table is this feature's, so the store that fills it is too. Without it the tenant keeps the default
        // that records nothing, and the voice loop's summaries are dropped rather than written to a missing table.
        services.Replace(ServiceDescriptor.Scoped<IAIVoiceSessionSummaryStore, YesSqlAIVoiceSessionSummaryStore>());
    }
}

/// <summary>
/// Registers services and configuration for the ToolInstances feature.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
public sealed class ToolInstancesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        // Tool-instance access is enforced when a profile, template, or chat interaction is configured, so
        // the default registry is used to surface the instances stored on the completion context at chat time
        // without re-checking permissions during the AI session.
        services.AddCrestAppsCore(crestApps => crestApps
            .AddAISuite(ai => ai
                .AddToolInstances(toolInstances => toolInstances
                    .AddHttpApiRequestSource()
                    .AddDocumentationSearchSources()
                    .AddYesSqlStores()
                )
            )
        );

        services.TryAddEnumerable(
        [
            ServiceDescriptor.Transient<IAIToolInstanceHandler, AlgoliaDocumentationToolInstanceExportHandler>(),
            ServiceDescriptor.Transient<IAIToolInstanceHandler, HttpApiRequestToolInstanceExportHandler>(),
        ]);

        services
            .AddDataMigration<AIToolInstanceIndexMigrations>()
            .AddDisplayDriver<AIToolInstance, AIToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, AIToolInstanceParametersDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, HttpApiRequestToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, SitemapDocumentationToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, SearchIndexDocumentationToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, AlgoliaDocumentationToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIToolInstance, WebsiteSearchToolInstanceDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfileToolInstancesDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplateToolInstancesDisplayDriver>()
            .AddNavigationProvider<AIToolInstanceAdminMenu>()
            .AddPermissionProvider<AIToolInstancePermissionsProvider>();
    }
}

/// <summary>
/// Registers the recipe execution step for the ToolInstances feature.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class ToolInstancesRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIToolInstanceStep>();
    }
}

/// <summary>
/// Registers the deployment step for the ToolInstances feature.
/// </summary>
[Feature(AIConstants.Feature.ToolInstances)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class ToolInstancesOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIToolInstanceDeploymentSource, AIToolInstanceDeploymentStep, AIToolInstanceDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Registers services and configuration for the Personas feature.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
public sealed class PersonasStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddCrestAppsCore(crestApps => crestApps
            .AddAISuite(ai => ai
                .AddPersonas(personas => personas
                    .AddYesSqlStores()
                )
            )
        );

        services
            .AddDataMigration<AIPersonaIndexMigrations>()
            .AddScoped<ICatalogEntryHandler<AIPersona>, AIPersonaDeletionHandler>()
            .AddDisplayDriver<AIPersona, AIPersonaDisplayDriver>()
            .AddDisplayDriver<AIProfile, AIProfilePersonaDisplayDriver>()
            .AddDisplayDriver<AIProfileTemplate, AIProfileTemplatePersonaDisplayDriver>()
            .AddNavigationProvider<AIPersonaAdminMenu>()
            .AddPermissionProvider<AIPersonaPermissionsProvider>();
    }
}

/// <summary>
/// Registers the recipe execution step for the Personas feature.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
[RequireFeatures("OrchardCore.Recipes.Core")]
public sealed class PersonasRecipesStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddRecipeExecutionStep<AIPersonaStep>();
    }
}

/// <summary>
/// Registers the deployment step for the Personas feature.
/// </summary>
[Feature(AIConstants.Feature.Personas)]
[RequireFeatures("OrchardCore.Deployment")]
public sealed class PersonasOCDeploymentsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddDeployment<AIPersonaDeploymentSource, AIPersonaDeploymentStep, AIPersonaDeploymentStepDisplayDriver>();
    }
}

/// <summary>
/// Adds the persona picker to the "AI completion with configuration" workflow task.
/// </summary>
[RequireFeatures("CrestApps.OrchardCore.AI.Chat.Interactions", AIConstants.Feature.Personas, "OrchardCore.Workflows")]
public sealed class ChatInteractionsPersonasWorkflowsStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddActivity<AICompletionWithConfigTask, AICompletionWithConfigPersonaDisplayDriver>();
    }
}
