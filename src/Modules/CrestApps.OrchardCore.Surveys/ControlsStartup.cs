using CrestApps.OrchardCore.Surveys.Core;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Drivers.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.ResourceManagement;

namespace CrestApps.OrchardCore.Surveys;

/// <summary>
/// Registers the built-in question types and their settings editors.
/// </summary>
public sealed class ControlsStartup : StartupBase
{
    /// <inheritdoc />
    public override void ConfigureServices(IServiceCollection services)
    {
        services.AddSurveysControls();
        services.AddTransient<IConfigureOptions<ResourceManagementOptions>, SurveysResourceManagementOptionsConfiguration>();
        services.AddScoped<IModularTenantEvents, SurveyControlRegistrationChecker>();

        services.AddDisplayDriver<SurveyQuestion, SingleChoiceSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, MultipleChoiceSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, BooleanSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, ShortTextSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, LongTextSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, NumberSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, DateSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, RatingSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, MatrixSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, RankingSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, MatchingSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, FillInBlanksSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, HiddenSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, CalculatedSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SliderSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, ConstantSumSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, SignatureSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, MultipleTextSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, TextBlockSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, ChoiceBlanksSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, MatrixDropdownSettingsDisplayDriver>();
        services.AddDisplayDriver<SurveyQuestion, ContactInfoSettingsDisplayDriver>();
    }
}
