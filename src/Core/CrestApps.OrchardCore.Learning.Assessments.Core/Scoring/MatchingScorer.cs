using System.Globalization;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores matching questions. The answers are shown shuffled under ids of their own, so the shown answers never reveal
/// which prompt they belong to.
/// </summary>
public sealed class MatchingScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Matching;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<MatchingQuestionPart>(nameof(MatchingQuestionPart)) ?? new MatchingQuestionPart();
        var pairs = part.Pairs.Where(pair => !string.IsNullOrEmpty(pair?.Id)).ToList();
        var answers = ScoringText.Shuffle(pairs, random)
            .Select((pair, index) => (Pair: pair, AnswerId: "m" + index.ToString(CultureInfo.InvariantCulture)))
            .ToList();

        item.Choices = pairs.Select(pair => new AttemptChoice { Id = pair.Id, Text = pair.Prompt }).ToList();
        item.Answers = answers.Select(answer => new AttemptChoice { Id = answer.AnswerId, Text = answer.Pair.Answer }).ToList();
        item.Key.Pairs = answers.ToDictionary(answer => answer.Pair.Id, answer => answer.AnswerId, StringComparer.Ordinal);
        item.Key.PartialCredit = part.PartialCredit;
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        if (item.Key.Pairs.Count == 0)
        {
            return 0;
        }

        var given = item.Response?.Pairs ?? new Dictionary<string, string>();
        var right = item.Key.Pairs.Count(pair => given.TryGetValue(pair.Key, out var answer) && string.Equals(answer, pair.Value, StringComparison.Ordinal));

        if (!item.Key.PartialCredit)
        {
            return right == item.Key.Pairs.Count ? item.Points : 0;
        }

        return ScoringText.Points(item.Points * right / item.Key.Pairs.Count, item.Points);
    }
}
