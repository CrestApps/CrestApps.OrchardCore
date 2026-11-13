using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores single choice questions: the points for the one right option, nothing otherwise.
/// </summary>
public sealed class SingleChoiceScorer : Services.IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.SingleChoice;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
        => ChoiceScoring.Snapshot(question, item, random);

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        var chosen = item.Response?.ChoiceIds ?? [];

        return chosen.Count == 1 && item.Key.ChoiceIds.Contains(chosen[0], StringComparer.Ordinal) ? item.Points : 0;
    }
}
