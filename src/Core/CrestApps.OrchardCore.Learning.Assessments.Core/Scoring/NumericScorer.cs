using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores numeric questions: an answer within the tolerance of the key earns the points.
/// </summary>
public sealed class NumericScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Numeric;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<NumericQuestionPart>(nameof(NumericQuestionPart)) ?? new NumericQuestionPart();

        item.Key.Number = part.Answer;
        item.Key.Tolerance = Math.Abs(part.Tolerance);
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        if (item.Response?.Number is not { } answer || item.Key.Number is not { } key)
        {
            return 0;
        }

        return Math.Abs(answer - key) <= item.Key.Tolerance ? item.Points : 0;
    }
}
