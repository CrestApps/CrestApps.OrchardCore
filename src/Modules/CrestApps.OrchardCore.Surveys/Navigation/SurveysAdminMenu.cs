using Microsoft.Extensions.Localization;
using OrchardCore.Navigation;

namespace CrestApps.OrchardCore.Surveys.Navigation;

/// <summary>
/// Adds the Surveys menu: surveys, question banks and grading.
/// </summary>
public sealed class SurveysAdminMenu : AdminNavigationProvider
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveysAdminMenu"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The localizer.</param>
    public SurveysAdminMenu(IStringLocalizer<SurveysAdminMenu> stringLocalizer)
    {
        S = stringLocalizer;
    }

    /// <inheritdoc />
    protected override ValueTask BuildAsync(NavigationBuilder builder)
    {
        builder.Add(S["Surveys"], "after", surveys => surveys
            .Id("surveys")
            .AddClass("surveys")
            .AddClass("icon-class-fa-solid")
            .AddClass("icon-class-fa-clipboard-list")
            .Add(S["Surveys"], S["Surveys"].PrefixPosition("1"), list => list
                .Id("surveysList")
                .Action("Index", "Surveys", SurveysConstants.Features.Surveys)
                .Permission(SurveysPermissions.ManageOwnSurveys)
                .LocalNav())
            .Add(S["Question banks"], S["Question banks"].PrefixPosition("2"), banks => banks
                .Id("surveysQuestionBanks")
                .Action("Index", "QuestionBanks", SurveysConstants.Features.Surveys)
                .Permission(SurveysPermissions.ViewQuestionBanks)
                .LocalNav())
            .Add(S["Templates"], S["Templates"].PrefixPosition("4"), templates => templates
                .Id("surveysTemplates")
                .Action("Index", "SurveyTemplates", SurveysConstants.Features.Surveys)
                .Permission(SurveysPermissions.ManageSurveyTemplates)
                .LocalNav())
            .Add(S["Grading"], S["Grading"].PrefixPosition("3"), grading => grading
                .Id("surveysGrading")
                .Action("Index", "SurveyGrading", SurveysConstants.Features.Surveys)
                .Permission(SurveysPermissions.GradeOwnSurveyResponses)
                .LocalNav()), priority: 1);

        return ValueTask.CompletedTask;
    }
}
