using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Core;

/// <summary>
/// Records a course survey or choice as submitted once the learner completed a response to its survey.
/// </summary>
public sealed class CourseSurveyResultProvider : ILearningSurveyItemResultProvider
{
    /// <inheritdoc />
    public string Kind => LearningSurveysConstants.SurveyKind;

    /// <inheritdoc />
    public CourseItemResult Build(ContentItem item, Enrollment enrollment, IReadOnlyList<SurveyResponse> responses)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (responses is null || !responses.Any(response => response.Status == SurveyResponseStatus.Completed))
        {
            return null;
        }

        return new CourseItemResult
        {
            ItemId = item.ContentItemId,
            Submitted = true,
        };
    }
}
