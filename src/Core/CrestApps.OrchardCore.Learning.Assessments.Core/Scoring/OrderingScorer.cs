using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores ordering questions. The items are shown shuffled; the key is their right order.
/// </summary>
public sealed class OrderingScorer : Services.IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Ordering;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<OrderingQuestionPart>(nameof(OrderingQuestionPart)) ?? new OrderingQuestionPart();
        var items = part.Items.Where(entry => !string.IsNullOrEmpty(entry?.Id)).ToList();

        item.Choices = ScoringText.Shuffle(items, random).Select(entry => new AttemptChoice { Id = entry.Id, Text = entry.Text }).ToList();
        item.Key.ChoiceIds = items.Select(entry => entry.Id).ToList();
        item.Key.PartialCredit = part.PartialCredit;
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        var key = item.Key.ChoiceIds;
        var given = item.Response?.ChoiceIds ?? [];

        if (key.Count == 0)
        {
            return 0;
        }

        var right = key.Where((id, index) => index < given.Count && string.Equals(given[index], id, StringComparison.Ordinal)).Count();

        if (!item.Key.PartialCredit)
        {
            return right == key.Count && given.Count == key.Count ? item.Points : 0;
        }

        return ScoringText.Points(item.Points * right / key.Count, item.Points);
    }
}
