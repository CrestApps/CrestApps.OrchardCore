using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Core.Surveys;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Services;

/// <summary>
/// Records a quiz's result from the learner's attempts: the attempts combined with the quiz's grading method, passed by
/// the survey's own rule (<see cref="SurveyPassRule"/>: its score bands, then its pass mark, else no pass decision), so
/// the course, the attempt and the credential rule agree; submitted once an attempt ended.
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
        var passed = SurveyPassRule.Decide(definition, score);

        return new CourseItemResult
        {
            ItemId = item.ContentItemId,
            Score = score,
            Passed = passed,
            PassDecided = true,
            Submitted = counted.Any(response => response.CompletedUtc is not null && response.Status is SurveyResponseStatus.Completed or SurveyResponseStatus.ScreenedOut),
            Graded = score is not null,
            Attempts = counted.Count(response => response.Status != SurveyResponseStatus.InProgress),
            ResetScore = score is null,
        };
    }
}
