using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores fill in the blanks questions. The text is split at its <c>[[answer]]</c> blanks: the segments are shown, the
/// answers become the key, and each blank earns its share of the points.
/// </summary>
public sealed partial class FillInBlanksScorer : Services.IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.FillInBlanks;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<FillInBlanksQuestionPart>(nameof(FillInBlanksQuestionPart)) ?? new FillInBlanksQuestionPart();
        var text = part.Text ?? string.Empty;
        var segments = new List<string>();
        var answers = new List<string>();
        var position = 0;

        foreach (Match match in Blank().Matches(text))
        {
            segments.Add(text[position..match.Index]);
            answers.Add(match.Groups["answers"].Value);
            position = match.Index + match.Length;
        }

        segments.Add(text[position..]);

        item.Segments = segments;
        item.BlankCount = answers.Count;
        item.Key.Texts = answers;
        item.Key.CaseSensitive = part.CaseSensitive;
        item.Key.PartialCredit = true;
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
    {
        if (item.Key.Texts.Count == 0)
        {
            return 0;
        }

        var given = item.Response?.Texts ?? [];
        var right = item.Key.Texts
            .Where((accepted, index) => index < given.Count && accepted.Split('|').Any(answer => ScoringText.Same(answer, given[index], item.Key.CaseSensitive)))
            .Count();

        return ScoringText.Points(item.Points * right / item.Key.Texts.Count, item.Points);
    }

    [GeneratedRegex(@"\[\[(?<answers>[^\]]+)\]\]")]
    private static partial Regex Blank();
}
