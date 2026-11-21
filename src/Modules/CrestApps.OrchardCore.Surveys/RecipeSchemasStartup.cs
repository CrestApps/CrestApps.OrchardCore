using CrestApps.OrchardCore.Recipes.Core;
using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using CrestApps.OrchardCore.Surveys.Core.Schemas;
using CrestApps.OrchardCore.Surveys.Deployments;
using CrestApps.OrchardCore.Surveys.Recipes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Describes the recipe steps, deployment steps and site settings of Surveys to CrestApps Recipes.
/// </summary>
[RequireFeatures(CrestAppsRecipesConstants.Feature.Area)]
public sealed class RecipeSchemasStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.TryAddScoped<SurveyDefinitionSchemaBuilder>();

        services.AddScoped<IRecipeStep, SurveysRecipeStepSchema>();
        services.AddScoped<IRecipeStep, SurveyTemplatesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, SurveyQuestionBanksRecipeStepSchema>();
        services.AddScoped<IRecipeStep, SurveyResponsesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, SurveyOverridesRecipeStepSchema>();
        services.AddScoped<IRecipeStep, SurveyInvitationsRecipeStepSchema>();
        services.AddScoped<ISiteSettingsSchemaDefinition, SurveySettingsSchema>();

        services.AddDeploymentStepSchema<SurveysDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SurveyTemplatesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SurveyQuestionBanksDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SurveyResponsesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SurveyOverridesDeploymentStepSchema>();
        services.AddDeploymentStepSchema<SurveyInvitationsDeploymentStepSchema>();
    }
}
