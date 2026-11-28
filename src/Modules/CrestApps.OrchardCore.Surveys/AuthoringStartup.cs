using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Drivers;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Navigation;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the authoring side of Surveys: the manager, validator and publisher, the designer, versions, collectors,
/// access settings and the site settings.
/// </summary>
public sealed class AuthoringStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysAuthoring();
        services.AddScoped<SurveyDesignerContext>();

        services.AddDisplayDriver<Survey, SurveyDisplayDriver>();
        services.AddDisplayDriver<Survey, SurveyDetailsDisplayDriver>();
        services.AddDisplayDriver<Survey, SurveyAccessDisplayDriver>();
        services.AddDisplayDriver<SurveyVersion, SurveyVersionDisplayDriver>();
        services.AddDisplayDriver<SurveyCollector, SurveyCollectorDisplayDriver>();

        services.AddDisplayDriver<SurveyQuestion, SurveyQuestionCommonDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SurveyChoicesDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SurveyRowsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SurveyValidationDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SurveyScoringDisplayDriver>();

        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionGeneralDisplayDriver>();
        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionScoringDisplayDriver>();
        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionEndingsDisplayDriver>();
        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionVariablesDisplayDriver>();
        services.AddDisplayDriver<SurveyDefinition, SurveyDefinitionCategoriesDisplayDriver>();

        services.AddSiteDisplayDriver<SurveySettingsDisplayDriver>();
        services.AddSiteSettingsPermission(SurveysConstants.SettingsGroupId, SurveysPermissions.ManageSurveySettings);
        services.AddNavigationProvider<SurveySettingsAdminMenu>();
    }
}
