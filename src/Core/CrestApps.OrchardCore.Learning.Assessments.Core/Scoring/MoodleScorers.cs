using System.Globalization;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Assessments.Services;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Scoring;

/// <summary>
/// Scores survey questions: every answer is accepted. At zero points the question is ungraded; with points, any answer
/// earns them and no answer earns nothing.
/// </summary>
public sealed class SurveyScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Survey;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<SurveyQuestionPart>(nameof(SurveyQuestionPart)) ?? new SurveyQuestionPart();

        item.Choices = part.Options
            .Where(option => !string.IsNullOrEmpty(option?.Id))
            .Select(option => new AttemptChoice { Id = option.Id, Text = option.Text, Feedback = option.Feedback })
            .ToList();
        item.Multiple = part.Multiple;
    }

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
        => item.Points > 0 && item.Response?.ChoiceIds?.Any(id => !string.IsNullOrEmpty(id)) == true ? item.Points : 0;
}

/// <summary>
/// Scores drag into text questions: the learner drags a word of the list into each blank.
/// </summary>
public sealed class DragIntoTextScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.DragIntoText;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
        => ChoiceBlanksScoring.Snapshot(question, item, random);

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
        => ChoiceBlanksScoring.Score(item);
}

/// <summary>
/// Scores gapselect questions: each blank is a drop-down list of the words.
/// </summary>
public sealed class GapSelectScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.GapSelect;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
        => ChoiceBlanksScoring.Snapshot(question, item, random);

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
        => ChoiceBlanksScoring.Score(item);
}

/// <summary>
/// A description between questions: nothing to answer and worth no points.
/// </summary>
public sealed class DescriptionScorer : IQuestionScorer
{
    /// <inheritdoc />
    public string QuestionType => AssessmentsConstants.QuestionTypes.Description;

    /// <inheritdoc />
    public void Snapshot(ContentItem question, AttemptItem item, Random random)
        => item.Points = 0;

    /// <inheritdoc />
    public decimal? Score(AttemptItem item)
        => 0;
}

/// <summary>
/// The snapshot and score shared by drag into text and gapselect questions. The text is split at its <c>[[word]]</c>
/// blanks; the words of the blanks and the distractors become one list of choices, shuffled or in alphabetical order
/// (never in blank order), under ids that follow that list (so neither the order nor the ids tell which word is right),
/// and the key holds the choice of each blank. Each blank earns its share.
/// </summary>
internal static partial class ChoiceBlanksScoring
{
    public static void Snapshot(ContentItem question, AttemptItem item, Random random)
    {
        var part = question.Get<ChoiceBlanksQuestionPart>(nameof(ChoiceBlanksQuestionPart)) ?? new ChoiceBlanksQuestionPart();
        var text = part.Text ?? string.Empty;
        var segments = new List<string>();
        var answers = new List<string>();
        var position = 0;

        foreach (Match match in Blank().Matches(text))
        {
            segments.Add(text[position..match.Index]);
            answers.Add(match.Groups["word"].Value.Trim());
            position = match.Index + match.Length;
        }

        segments.Add(text[position..]);

        var words = answers.Concat((part.Distractors ?? []).Select(word => word?.Trim()))
            .Where(word => !string.IsNullOrEmpty(word))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // Shuffled, or else in alphabetical order: the words of the blanks in blank order would read off the answers,
        // and the choice ids follow the order shown, never the blanks.
        words = part.ShuffleChoices
            ? ScoringText.Shuffle(words, random)
            : [.. words.Order(StringComparer.OrdinalIgnoreCase).ThenBy(word => word, StringComparer.Ordinal)];

        var choices = words.Select((word, index) => new AttemptChoice { Id = "c" + index.ToString(CultureInfo.InvariantCulture), Text = word }).ToList();
        var ids = choices.ToDictionary(choice => choice.Text, choice => choice.Id, StringComparer.Ordinal);

        item.Segments = segments;
        item.BlankCount = answers.Count;
        item.Choices = choices;
        item.Key.ChoiceIds = answers.Select(answer => ids.GetValueOrDefault(answer, string.Empty)).ToList();
        item.Key.PartialCredit = true;
    }

    public static decimal? Score(AttemptItem item)
    {
        if (item.Key.ChoiceIds.Count == 0)
        {
            return 0;
        }

        var given = item.Response?.ChoiceIds ?? [];
        var right = item.Key.ChoiceIds
            .Where((id, index) => index < given.Count && !string.IsNullOrEmpty(id) && string.Equals(id, given[index], StringComparison.Ordinal))
            .Count();

        return ScoringText.Points(item.Points * right / item.Key.ChoiceIds.Count, item.Points);
    }

    [GeneratedRegex(@"\[\[(?<word>[^\]]+)\]\]")]
    private static partial Regex Blank();
}
