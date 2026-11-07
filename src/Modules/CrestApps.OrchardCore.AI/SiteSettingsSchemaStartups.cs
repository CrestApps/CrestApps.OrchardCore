using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Workflows.Activities;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI;

/// <summary>
/// Registers the JSON schemas of the AI workflow activities, so the Recipes module can describe them.
/// </summary>
[Feature(AIConstants.Feature.Area)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class AIWorkflowRecipeSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<AIChatSessionAllFieldsExtractedEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionClosedEventSchema>()
            .AddWorkflowActivitySchema<AIConversationStartingEventSchema>()
            .AddWorkflowActivitySchema<SetConversationVariablesTaskSchema>()
            .AddWorkflowActivitySchema<UseAIProfileTaskSchema>()
            .AddWorkflowActivitySchema<DeclineConversationTaskSchema>()
            .AddWorkflowActivitySchema<AIToolCalledEventSchema>()
            .AddWorkflowActivitySchema<AIConversationTurnEventSchema>()
            .AddWorkflowActivitySchema<SplitTrafficTaskSchema>()
            .AddWorkflowActivitySchema<EvaluateConversationTaskSchema>()
            .AddWorkflowActivitySchema<AIConversationEvaluatedEventSchema>()
            .AddWorkflowActivitySchema<SimulateConversationTaskSchema>()
            .AddWorkflowActivitySchema<RerunConversationAnalysisTaskSchema>()
            .AddWorkflowActivitySchema<CheckConversationMetricTaskSchema>()
            .AddWorkflowActivitySchema<UpdateContactMemoryTaskSchema>()
            .AddWorkflowActivitySchema<SendSignedWebhookTaskSchema>()
            .AddWorkflowActivitySchema<AIGuardrailTriggeredEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionFieldExtractedEventSchema>()
            .AddWorkflowActivitySchema<AIChatSessionPostProcessedEventSchema>()
            .AddWorkflowActivitySchema<AICompletionFromProfileTaskSchema>()
            .AddWorkflowActivitySchema<AICompletionWithConfigTaskSchema>();
    }
}

/// <summary>
/// Registers the JSON schemas of the general AI and default deployment site settings, so the Recipes module can describe them.
/// </summary>
[Feature(AIConstants.Feature.Area)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AISiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, GeneralAISettingsSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, DefaultAIDeploymentSettingsSchema>();
    }
}

/// <summary>
/// Registers the JSON schema of the default orchestrator site settings, so the Recipes module can describe them.
/// </summary>
[Feature(AIConstants.Feature.ChatCore)]
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class AIChatCoreSiteSettingsSchemaStartup : StartupBase
{
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, DefaultOrchestratorSettingsSchema>();
    }
}
