using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Records a quiz's result from the learner's attempts: the attempts combined with the quiz's grading method, passed
/// against the pass mark of the survey, submitted once an attempt ended.
/// </summary>
public sealed class QuizResultProvider : ILearningSurveyItemResultProvider
{
    /// <inheritdoc />
    public string Kind => AssessmentsConstants.QuizKind;

    /// <inheritdoc />
    public CourseItemResult Build(ContentItem item, Enrollment enrollment, SurveyDefinition definition, IReadOnlyList<SurveyResponse> responses)
    {
        ArgumentNullException.ThrowIfNull(item);

        var method = item.TryGet<QuizPart>(out var part) ? part.GradingMethod : GradingMethod.Highest;
        var counted = (responses ?? []).Where(response => response.Status != SurveyResponseStatus.Voided).ToList();
        var score = QuizGradeCalculator.Calculate(counted, method);
        var passMark = definition?.Scoring?.PassMarkPercent;
        bool? passed = score is null ? null : passMark is null || score >= passMark;

        return new CourseItemResult
        {
            ItemId = item.ContentItemId,
            Score = score,
            Passed = passed,
            Submitted = counted.Any(response => response.CompletedUtc is not null && response.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.ScreenedOut),
            Graded = score is not null,
            Attempts = counted.Count(response => response.Status != SurveyResponseStatus.InProgress),
            ResetScore = score is null,
        };
    }
}
