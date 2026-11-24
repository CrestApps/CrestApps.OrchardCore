using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores multiple choice questions: all or nothing, or with partial credit the share of right choices less the share
/// of wrong ones.
/// </summary>
public sealed class MultipleChoiceScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.MultipleChoice;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
        => ChoiceScoring.Snapshot(question, item, random);

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        var key = item.Key.ChoiceIds.ToHashSet(StringComparer.Ordinal);
        var chosen = (item.Response?.ChoiceIds ?? []).ToHashSet(StringComparer.Ordinal);

        if (key.Count == 0)
        {
            return 0;
        }

        if (!item.Key.PartialCredit)
        {
            return key.SetEquals(chosen) ? item.Points : 0;
        }

        var right = chosen.Count(key.Contains);
        var wrong = chosen.Count - right;

        return ScoringText.Points(item.Points * (right - wrong) / key.Count, item.Points);
    }
}
