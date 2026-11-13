using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores short text questions against the accepted answers, trimmed, with runs of spaces collapsed.
/// </summary>
public sealed class ShortTextScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.ShortText;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<ShortTextQuestionPart>(nameof(ShortTextQuestionPart)) ?? new ShortTextQuestionPart();

        item.Key.Texts = part.AcceptedAnswers.Where(answer => !string.IsNullOrWhiteSpace(answer)).ToList();
        item.Key.CaseSensitive = part.CaseSensitive;
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        var answer = item.Response?.Texts?.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(answer))
        {
            return 0;
        }

        return item.Key.Texts.Any(accepted => ScoringText.Same(accepted, answer, item.Key.CaseSensitive)) ? item.Points : 0;
    }
}
