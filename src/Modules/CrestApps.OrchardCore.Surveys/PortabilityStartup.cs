using CrestApps.OrchardCore.Surveys.Deployments;
using CrestApps.OrchardCore.Surveys.Recipes;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Deployment;
using OrchardCore.Modules;
using OrchardCore.Recipes;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the recipe and deployment steps of Surveys, and the services that write and read their records.
/// </summary>
public sealed class PortabilityStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddScoped<ISurveyUserLookup, SurveyUserLookup>();
        services.TryAddScoped<ISurveyAuditTrail, NullSurveyAuditTrail>();
        services.TryAddScoped<SurveysRecipeExporter>();
        services.TryAddScoped<SurveysExportAuthorizer>();

        services.AddRecipeExecutionStep<SurveysRecipeStep>();
        services.AddRecipeExecutionStep<SurveyTemplatesRecipeStep>();
        services.AddRecipeExecutionStep<SurveyQuestionBanksRecipeStep>();
        services.AddRecipeExecutionStep<SurveyResponsesRecipeStep>();
        services.AddRecipeExecutionStep<SurveyOverridesRecipeStep>();
        services.AddRecipeExecutionStep<SurveyInvitationsRecipeStep>();

        services.AddDeployment<SurveysDeploymentSource, SurveysDeploymentStep, SurveysDeploymentStepDisplayDriver>();
        services.AddDeployment<SurveyTemplatesDeploymentSource, SurveyTemplatesDeploymentStep, SurveyTemplatesDeploymentStepDisplayDriver>();
        services.AddDeployment<SurveyQuestionBanksDeploymentSource, SurveyQuestionBanksDeploymentStep, SurveyQuestionBanksDeploymentStepDisplayDriver>();
        services.AddDeployment<SurveyResponsesDeploymentSource, SurveyResponsesDeploymentStep, SurveyResponsesDeploymentStepDisplayDriver>();
        services.AddDeployment<SurveyOverridesDeploymentSource, SurveyOverridesDeploymentStep, SurveyOverridesDeploymentStepDisplayDriver>();
        services.AddDeployment<SurveyInvitationsDeploymentSource, SurveyInvitationsDeploymentStep, SurveyInvitationsDeploymentStepDisplayDriver>();
    }
}
