using CrestApps.OrchardCore.Dispatch.Recipes;
using CrestApps.OrchardCore.Dispatch.Recipes.Workflows;
using CrestApps.OrchardCore.Recipes.Core;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Dispatch;

/// <summary>
/// Describes the Dispatch site settings to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipesSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<ISiteSettingsSchemaDefinition, DispatchSettingsSchema>();
    }
}

/// <summary>
/// Describes the visit workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowsRecipeSchemaStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<VisitAssignedEventSchema>()
            .AddWorkflowActivitySchema<VisitStatusChangedEventSchema>()
            .AddWorkflowActivitySchema<VisitAtRiskEventSchema>();
    }
}
