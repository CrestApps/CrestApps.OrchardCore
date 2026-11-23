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
/// the course, the attempt and the credential rule agree; submitted once an attempt ended. A method that picks one attempt
/// takes that attempt's own pass, decided against its own version.
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
        var passed = Passed(counted, method, definition, score);

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

    /// <summary>
    /// Decides the pass of the combined score. When the grading method picks one attempt (highest, latest or first), the
    /// score is that attempt's, and so is the pass: Surveys decided it with the one pass rule against the version that
    /// attempt ran on, so a later version's higher pass mark never fails an attempt that passed, and the item agrees with
    /// the attempt and its credential. An average belongs to no attempt, so it is decided with the one pass rule against
    /// the given definition (the version of the latest graded attempt).
    /// </summary>
    /// <param name="counted">The attempts that count.</param>
    /// <param name="method">The grading method.</param>
    /// <param name="definition">The definition of the version of the latest graded attempt.</param>
    /// <param name="score">The combined score.</param>
    /// <returns>Whether the score passes, or <see langword="null"/> when nothing decides it.</returns>
    internal static bool? Passed(IReadOnlyList<SurveyResponse> counted, GradingMethod method, SurveyDefinition definition, decimal? score)
    {
        if (score is null)
        {
            return null;
        }

        var graded = counted
            .Where(response => response.Status == SurveyResponseStatus.Completed && response.Grading == GradingStatus.Graded && response.Percent is not null)
            .OrderBy(response => response.CompletedUtc ?? response.StartedUtc)
            .ToList();

        var chosen = method switch
        {
            GradingMethod.Latest => graded.LastOrDefault(),
            GradingMethod.First => graded.FirstOrDefault(),
            GradingMethod.Average => null,

            // Of the attempts with the highest score, one that passed: a tie never fails the learner.
            _ => graded
                .Where(response => response.Percent == score)
                .OrderByDescending(response => response.Passed == true)
                .FirstOrDefault(),
        };

        // An attempt with no pass decision of its own (scored before Surveys stored one) is decided as an average is.
        return chosen?.Passed ?? SurveyPassRule.Decide(definition, score);
    }
}
