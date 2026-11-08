using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Wizard.Recipes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Wizard;

/// <summary>
/// Describes the wizard workflow events to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Workflows)]
public sealed class WorkflowRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services
            .AddWorkflowActivitySchema<WizardStartedEventSchema>()
            .AddWorkflowActivitySchema<WizardStepDisplayedEventSchema>()
            .AddWorkflowActivitySchema<WizardCompletedEventSchema>()
            .AddWorkflowActivitySchema<WizardFailedEventSchema>();
    }
}

/// <summary>
/// Describes the wizard content part to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area, OrchardCoreFeatureIds.Contents)]
public sealed class ContentsRecipeSchemasStartup : StartupBase
{
    /// <inheritdoc/>
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddScoped<IContentSchemaDefinition, WizardPartSchemaDefinition>();
    }
}
