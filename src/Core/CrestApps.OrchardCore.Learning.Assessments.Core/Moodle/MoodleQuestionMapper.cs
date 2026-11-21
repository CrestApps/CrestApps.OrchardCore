using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys.Core.Qti;
using CrestApps.OrchardCore.Learning.Core.Moodle;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Moodle;

/// <summary>
/// Why a Moodle question was not imported.
/// </summary>
public static class MoodleQuestionRefusals
{
    /// <summary>The question type has no question type on the site; the detail names it.</summary>
    public const string UnsupportedType = "UnsupportedType";

    /// <summary>The question text shows a picture or media, which the import does not bring yet.</summary>
    public const string Picture = "Picture";

    /// <summary>An answer holds a picture or media where the question type keeps plain text.</summary>
    public const string MarkupInAnswer = "MarkupInAnswer";

    /// <summary>A matching question has answers that match no question (distractors).</summary>
    public const string Distractors = "Distractors";

    /// <summary>The question has no answer worth the full mark.</summary>
    public const string NoRightAnswer = "NoRightAnswer";

    /// <summary>An answer cannot be kept; the detail is the answer.</summary>
    public const string UnsupportedAnswer = "UnsupportedAnswer";
}

/// <summary>
/// A Moodle question as a question of the site, or why it is not one.
/// </summary>
/// <param name="Item">The item: the question when it maps, and its identifier, title and source file either way.</param>
/// <param name="Refusal">Why it does not map, one of <see cref="MoodleQuestionRefusals"/>, or <see langword="null"/>.</param>
/// <param name="Detail">What the refusal is about, such as the question type.</param>
public sealed record MoodleQuestionMapping(QtiItem Item, string Refusal, string Detail);

/// <summary>
/// Maps a Moodle question onto the question model the QTI import writes question rows from (<see cref="QtiQuestion"/>),
/// so a Moodle question is imported by the same question columns and handlers as a QTI item. Multiple choice becomes
/// single or multiple choice (a survey question when no answer is worth points, so every answer counts), true/false,
/// short answer, numerical, essay and matching their own types, drag into text and gap select their own types with the
/// choices no blank uses as distractors, and a description a description worth nothing. The question text stays HTML;
/// choices, answers and blanks become plain text.
/// </summary>
public static partial class MoodleQuestionMapper
{
    private const string PluginFile = "@@PLUGINFILE@@";

    /// <summary>
    /// Maps a question.
    /// </summary>
    /// <param name="question">The question.</param>
    /// <param name="sourceIdentifier">The identifier the question keeps, to be updated by a later import.</param>
    /// <param name="sourceFile">Where the question came from, such as <c>course.mbz/questions.xml</c>.</param>
    /// <returns>The question, or why it is refused.</returns>
    public static MoodleQuestionMapping Map(MoodleQuestion question, string sourceIdentifier, string sourceFile)
    {
        ArgumentNullException.ThrowIfNull(question);

        var item = new QtiItem
        {
            Identifier = sourceIdentifier,
            Title = string.IsNullOrWhiteSpace(question.Name) ? sourceIdentifier : question.Name.Trim(),
            Path = sourceFile,
        };

        if (HasMedia(question.Text))
        {
            return new MoodleQuestionMapping(item, MoodleQuestionRefusals.Picture, null);
        }

        var mapped = new QtiQuestion
        {
            Prompt = question.Text,
            Points = question.DefaultMark > 0 ? question.DefaultMark : 1,
            GeneralFeedback = string.IsNullOrWhiteSpace(question.GeneralFeedback) ? null : WithoutMedia(question.GeneralFeedback),
        };

        (string Code, string Detail) refusal = question.Type switch
        {
            "multichoice" => MultipleChoice(question, mapped),
            "truefalse" => TrueFalse(question, mapped),
            "shortanswer" => ShortAnswer(question, mapped),
            "numerical" => Numerical(question, mapped),
            "essay" => Essay(question, mapped),
            "match" => Matching(question, mapped),
            "ddwtos" => Blanks(question, mapped, QtiQuestionTypes.DragIntoText),
            "gapselect" => Blanks(question, mapped, QtiQuestionTypes.GapSelect),
            "description" => Description(mapped),
            _ => (MoodleQuestionRefusals.UnsupportedType, question.Type),
        };

        if (refusal.Code is not null)
        {
            return new MoodleQuestionMapping(item, refusal.Code, refusal.Detail);
        }

        item.Question = mapped;

        return new MoodleQuestionMapping(item, null, null);
    }

    private static (string Code, string Detail) MultipleChoice(MoodleQuestion question, QtiQuestion mapped)
    {
        if (question.Answers.Any(answer => HasMedia(answer.Text)))
        {
            return (MoodleQuestionRefusals.MarkupInAnswer, null);
        }

        var single = question.Option("single") != "0";
        var best = question.Answers.Select(answer => answer.Fraction).DefaultIfEmpty(0).Max();

        if (question.Answers.Count == 0)
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        // No answer worth points: Moodle's way of asking an opinion. Every answer is accepted.
        if (best <= 0)
        {
            mapped.QuestionType = single ? QtiQuestionTypes.SingleChoice : QtiQuestionTypes.MultipleChoice;
            mapped.Multiple = !single;
            mapped.Points = 0;
            mapped.Options = [.. question.Answers.Select(answer => new QtiOption { Identifier = answer.Id, Text = Plain(answer.Text) })];

            return default;
        }

        mapped.QuestionType = single ? QtiQuestionTypes.SingleChoice : QtiQuestionTypes.MultipleChoice;
        mapped.Shuffle = question.Option("shuffleanswers") == "1";

        foreach (var answer in question.Answers)
        {
            mapped.Options.Add(new QtiOption
            {
                Identifier = answer.Id,
                Text = Plain(answer.Text),
                // A single answer question has the best answer right; a multiple answer one every answer worth points.
                Correct = single ? answer.Fraction == best : answer.Fraction > 0,
                Feedback = string.IsNullOrWhiteSpace(answer.Feedback) ? null : Plain(answer.Feedback),
            });
        }

        mapped.PartialCredit = !single && mapped.Options.Count(option => option.Correct) > 1;

        return default;
    }

    private static (string Code, string Detail) TrueFalse(MoodleQuestion question, QtiQuestion mapped)
    {
        var right = question.Answers.FirstOrDefault(answer => answer.Fraction > 0);

        if (right is null)
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        mapped.QuestionType = QtiQuestionTypes.TrueFalse;
        mapped.TrueFalseAnswer = question.Option("trueanswer") is { } trueAnswer
            ? right.Id == trueAnswer
            : string.Equals(Plain(right.Text), "True", StringComparison.OrdinalIgnoreCase);

        return default;
    }

    private static (string Code, string Detail) ShortAnswer(MoodleQuestion question, QtiQuestion mapped)
    {
        var accepted = question.Answers.Where(answer => answer.Fraction >= 1).Select(answer => Plain(answer.Text)).Where(text => !string.IsNullOrEmpty(text)).ToList();

        if (accepted.Count == 0)
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        // Moodle matches * as any text; a short text question matches the answer as it is.
        if (accepted.FirstOrDefault(answer => answer.Contains('*', StringComparison.Ordinal)) is { } wildcard)
        {
            return (MoodleQuestionRefusals.UnsupportedAnswer, wildcard);
        }

        mapped.QuestionType = QtiQuestionTypes.ShortText;
        mapped.AcceptedAnswers = accepted;
        mapped.CaseSensitive = question.Option("usecase") == "1";

        return default;
    }

    private static (string Code, string Detail) Numerical(MoodleQuestion question, QtiQuestion mapped)
    {
        var right = question.Answers.FirstOrDefault(answer => answer.Fraction >= 1);

        if (right is null)
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        if (!decimal.TryParse(Plain(right.Text), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            return (MoodleQuestionRefusals.UnsupportedAnswer, Plain(right.Text));
        }

        mapped.QuestionType = QtiQuestionTypes.Numeric;
        mapped.NumericAnswer = value;
        mapped.Tolerance = right.Id is not null && question.Tolerances.TryGetValue(right.Id, out var tolerance) ? Math.Abs(tolerance) : 0;

        return default;
    }

    private static (string Code, string Detail) Essay(MoodleQuestion question, QtiQuestion mapped)
    {
        mapped.QuestionType = QtiQuestionTypes.Essay;
        mapped.GraderGuidance = string.IsNullOrWhiteSpace(question.Option("graderinfo")) ? null : Plain(question.Option("graderinfo"));

        return default;
    }

    private static (string Code, string Detail) Matching(MoodleQuestion question, QtiQuestion mapped)
    {
        if (question.Matches.Any(match => string.IsNullOrWhiteSpace(Plain(match.Question))))
        {
            return (MoodleQuestionRefusals.Distractors, null);
        }

        if (question.Matches.Count == 0)
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        if (question.Matches.Any(match => HasMedia(match.Question) || HasMedia(match.Answer)))
        {
            return (MoodleQuestionRefusals.MarkupInAnswer, null);
        }

        mapped.QuestionType = QtiQuestionTypes.Matching;
        mapped.Pairs = [.. question.Matches.Select(match => (Plain(match.Question), Plain(match.Answer)))];
        mapped.PartialCredit = true;

        return default;
    }

    // A description has nothing to answer and is worth no points; its text is the prompt.
    private static (string Code, string Detail) Description(QtiQuestion mapped)
    {
        mapped.QuestionType = QtiQuestionTypes.Description;
        mapped.Points = 0;

        return default;
    }

    // Drag into text and gap select put [[n]] in the text, n the place of the right choice among the answers. The blank
    // becomes the right choice's text, and the choices no blank uses are the distractors offered with the right ones.
    private static (string Code, string Detail) Blanks(MoodleQuestion question, QtiQuestion mapped, string questionType)
    {
        if (question.Answers.Any(answer => HasMedia(answer.Text)))
        {
            return (MoodleQuestionRefusals.MarkupInAnswer, null);
        }

        var choices = question.Answers.Select(answer => Plain(answer.Text)).ToList();
        string unsupported = null;
        var text = BlankPattern().Replace(Plain(question.Text), match =>
        {
            var index = int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) - 1;

            if (index < 0 || index >= choices.Count || string.IsNullOrEmpty(choices[index]))
            {
                unsupported ??= match.Value;

                return match.Value;
            }

            if (choices[index].IndexOfAny(['|', '[', ']']) >= 0)
            {
                unsupported ??= choices[index];
            }

            return "[[" + choices[index] + "]]";
        });

        if (unsupported is not null)
        {
            return (MoodleQuestionRefusals.UnsupportedAnswer, unsupported);
        }

        if (!BlankPattern().IsMatch(Plain(question.Text)))
        {
            return (MoodleQuestionRefusals.NoRightAnswer, null);
        }

        var used = BlankPattern().Matches(Plain(question.Text)).Select(match => int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture) - 1).ToHashSet();

        mapped.QuestionType = questionType;
        mapped.Prompt = null;
        mapped.BlankText = text;
        mapped.Shuffle = question.Option("shuffleanswers") == "1";
        mapped.Distractors = [.. choices.Where((choice, index) => !used.Contains(index) && !string.IsNullOrEmpty(choice) && !choices.Where((_, other) => used.Contains(other)).Contains(choice, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal)];

        return default;
    }

    private static bool HasMedia(string html)
        => !string.IsNullOrEmpty(html) && (html.Contains(PluginFile, StringComparison.Ordinal) || MediaPattern().IsMatch(html));

    private static string WithoutMedia(string html)
        => MediaElementPattern().Replace(html, string.Empty);

    /// <summary>
    /// Turns HTML into plain text: tags removed, entities decoded and white space collapsed.
    /// </summary>
    /// <param name="html">The HTML.</param>
    /// <returns>The text.</returns>
    public static string Plain(string html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }

        var text = WebUtility.HtmlDecode(TagPattern().Replace(BlockPattern().Replace(html, " "), string.Empty));

        return WhiteSpacePattern().Replace(text, " ").Trim();
    }

    [GeneratedRegex(@"\[\[(?<n>\d+)\]\]")]
    private static partial Regex BlankPattern();

    [GeneratedRegex(@"<\s*(img|video|audio|iframe|object|embed|math|svg)\b", RegexOptions.IgnoreCase)]
    private static partial Regex MediaPattern();

    [GeneratedRegex(@"<\s*(img|video|audio|iframe|object|embed)\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex MediaElementPattern();

    [GeneratedRegex(@"<\s*/?\s*(p|div|br|li|tr|h[1-6])\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockPattern();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhiteSpacePattern();
}
