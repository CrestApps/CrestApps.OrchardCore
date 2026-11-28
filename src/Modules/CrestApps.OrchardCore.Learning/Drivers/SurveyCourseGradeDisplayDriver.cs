using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.ViewModels;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Drivers;

/// <summary>
/// Warns on the access and settings page of a survey that a course item owns, when that item counts toward the course
/// grade and the survey is anonymous: an anonymous survey records the learner's completion only, with no score, so the
/// item stays out of the course grade and the other items are weighed without it.
/// </summary>
internal sealed class SurveyCourseGradeDisplayDriver : DisplayDriver<Survey>
{
    private readonly ILearningSurveyService _learningSurveys;

    internal readonly IStringLocalizer S;

    public SurveyCourseGradeDisplayDriver(
        ILearningSurveyService learningSurveys,
        IStringLocalizer<SurveyCourseGradeDisplayDriver> stringLocalizer)
    {
        _learningSurveys = learningSurveys;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public override IDisplayResult Edit(Survey survey, BuildEditorContext context)
    {
        // A survey no course item owns has no course grade to warn about: no content query runs for it.
        if (!LearningSurveyContexts.IsContentOwner(survey?.Owner))
        {
            return null;
        }

        // The warning sits at the top of the Anonymity card of the Surveys module, beside the choice it is about.
        return Initialize<SurveyCourseGradeViewModel>("SurveyCourseGrade_Edit", async model =>
        {
            var item = await _learningSurveys.FindLatestOwnerItemAsync(survey);

            model.Gradable = item?.TryGet<CourseItemPart>(out var part) == true && part.Gradable;
            model.Anonymous = survey.Access?.Anonymity == AnonymityMode.Anonymous;
        }).Location(l => l.Zone("Content", "4").Card(S["Anonymity"], "30")).OnGroup(SurveysConstants.AccessEditorGroupId);
    }
}
