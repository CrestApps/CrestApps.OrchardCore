using CrestApps.Core;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Infrastructure.Html;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// The prompt, points, difficulty, tags, general feedback and source of every question.
/// </summary>
internal sealed class QuestionPartImportHandler : QuestionColumnsImportHandler<QuestionPart>
{
    public QuestionPartImportHandler(IHtmlSanitizerService htmlSanitizer, IStringLocalizer<QuestionPartImportHandler> S)
    {
        // The prompt and feedback are shown as HTML, so an imported value is sanitized as the editor sanitizes it.
        string Sanitize(string html) => string.IsNullOrWhiteSpace(html) ? null : htmlSanitizer.Sanitize(html);

        Columns =
        [
            new("Prompt", S["The question, as text or HTML."], (part, value) => part.Prompt = Sanitize(value), part => part.Prompt),
            new("Points", S["The points of the question, a number."], (part, value) => part.Points = Decimal(value) ?? part.Points, part => Text(part.Points)),
            new("Difficulty", S["The difficulty from 1 (easy) to 5 (hard)."], (part, value) => part.Difficulty = int.TryParse(value, out var difficulty) ? Math.Clamp(difficulty, 1, 5) : part.Difficulty, part => part.Difficulty.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Tags", S["Tags separated by |, used by random draws."], (part, value) => part.Tags = List(value), part => Join(part.Tags)),
            new("GeneralFeedback", S["The feedback shown on review."], (part, value) => part.GeneralFeedback = Sanitize(value), part => part.GeneralFeedback),
            new("SourceIdentifier", S["The identifier of the question in the system it came from, such as a QTI item identifier. A row with a source identifier updates the question imported before with it into the same question bank."], (part, value) => part.SourceIdentifier = value, part => part.SourceIdentifier),
            new("SourceFile", S["The file the question was imported from."], (part, value) => part.SourceFile = value, part => part.SourceFile),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<QuestionPart>> Columns { get; }
}

/// <summary>
/// The options of a single or multiple choice question: separated by |, the right ones starting with *, and the feedback
/// of each option in a list that lines up with them.
/// </summary>
internal sealed class ChoiceQuestionPartImportHandler : QuestionColumnsImportHandler<ChoiceQuestionPart>
{
    public ChoiceQuestionPartImportHandler(IStringLocalizer<ChoiceQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Options", S["The options separated by |; start each right option with *, such as: Foam|*CO2|Water. Write \\| for a | in an option and \\* for an option that starts with a star."], (part, value) =>
            {
                var feedback = part.Options.ToDictionary(option => option.Text ?? string.Empty, option => option.Feedback, StringComparer.Ordinal);
                part.Options = QuestionCells.SplitRaw(value).Select(raw =>
                {
                    var correct = QuestionCells.IsMarkedRight(raw, out var text);

                    return new ChoiceOption
                    {
                        Id = UniqueId.GenerateId(),
                        Text = text,
                        Correct = correct,
                        Feedback = feedback.GetValueOrDefault(text),
                    };
                }).ToList();
            }, part => QuestionCells.Join(part.Options.Select(option => (option.Correct ? QuestionCells.RightMark.ToString() : string.Empty) + QuestionCells.EscapeValue(option.Text)))),
            new("OptionFeedback", S["The feedback of each option, shown on review when it was chosen, separated by | in the order of the options; leave a value empty for an option without feedback."], (part, value) =>
            {
                var feedback = QuestionCells.Split(value, keepEmpty: true);

                for (var i = 0; i < part.Options.Count; i++)
                {
                    part.Options[i].Feedback = i < feedback.Count && feedback[i].Length > 0 ? feedback[i] : null;
                }
            }, part => part.Options.Any(option => !string.IsNullOrEmpty(option.Feedback)) ? QuestionCells.Join(part.Options.Select(option => QuestionCells.EscapeValue(option.Feedback))) : null),
            new("ShuffleOptions", S["Whether the options are shown in a random order: yes or no."], (part, value) => part.ShuffleOptions = Boolean(value), part => part.ShuffleOptions ? "yes" : "no"),
            new("PartialCredit", S["Whether a multiple choice answer gets part of the points for part of the right options: yes or no."], (part, value) => part.PartialCredit = Boolean(value), part => part.PartialCredit ? "yes" : "no"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<ChoiceQuestionPart>> Columns { get; }
}

/// <summary>
/// The answer of a true or false question.
/// </summary>
internal sealed class TrueFalseQuestionPartImportHandler : QuestionColumnsImportHandler<TrueFalseQuestionPart>
{
    public TrueFalseQuestionPartImportHandler(IStringLocalizer<TrueFalseQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Answer", S["The right answer: true or false."], (part, value) => part.Answer = Boolean(value), part => part.Answer ? "true" : "false"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<TrueFalseQuestionPart>> Columns { get; }
}

/// <summary>
/// The accepted answers of a short text question.
/// </summary>
internal sealed class ShortTextQuestionPartImportHandler : QuestionColumnsImportHandler<ShortTextQuestionPart>
{
    public ShortTextQuestionPartImportHandler(IStringLocalizer<ShortTextQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("AcceptedAnswers", S["The accepted answers separated by |."], (part, value) => part.AcceptedAnswers = List(value), part => Join(part.AcceptedAnswers)),
            new("CaseSensitive", S["Whether capital letters must match: yes or no."], (part, value) => part.CaseSensitive = Boolean(value), part => part.CaseSensitive ? "yes" : "no"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<ShortTextQuestionPart>> Columns { get; }
}

/// <summary>
/// The answer and tolerance of a numeric question.
/// </summary>
internal sealed class NumericQuestionPartImportHandler : QuestionColumnsImportHandler<NumericQuestionPart>
{
    public NumericQuestionPartImportHandler(IStringLocalizer<NumericQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Answer", S["The right number, written with a dot."], (part, value) => part.Answer = Decimal(value) ?? part.Answer, part => Text(part.Answer)),
            new("Tolerance", S["How far from the right number an answer may be."], (part, value) => part.Tolerance = Decimal(value) ?? part.Tolerance, part => Text(part.Tolerance)),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<NumericQuestionPart>> Columns { get; }
}

/// <summary>
/// The pairs of a matching question: <c>prompt=answer</c>, separated by |.
/// </summary>
internal sealed class MatchingQuestionPartImportHandler : QuestionColumnsImportHandler<MatchingQuestionPart>
{
    public MatchingQuestionPartImportHandler(IStringLocalizer<MatchingQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Pairs", S["The pairs as prompt=answer separated by |, such as: France=Paris|Italy=Rome. Write \\= for an = in a prompt or an answer and \\| for a |."], (part, value) => part.Pairs = QuestionCells.SplitRaw(value)
                .Select(raw => QuestionCells.TrySplitPair(raw, out var prompt, out var answer) ? new MatchingPair { Id = UniqueId.GenerateId(), Prompt = prompt, Answer = answer } : null)
                .Where(pair => pair is not null)
                .ToList(), part => QuestionCells.Join(part.Pairs.Select(pair => QuestionCells.EscapeValue(pair.Prompt, pair: true) + QuestionCells.PairSeparator + QuestionCells.EscapeValue(pair.Answer, pair: true)))),
            new("PartialCredit", S["Whether each right match earns its share of the points: yes or no."], (part, value) => part.PartialCredit = Boolean(value), part => part.PartialCredit ? "yes" : "no"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<MatchingQuestionPart>> Columns { get; }
}

/// <summary>
/// The items of an ordering question, in their right order, separated by |.
/// </summary>
internal sealed class OrderingQuestionPartImportHandler : QuestionColumnsImportHandler<OrderingQuestionPart>
{
    public OrderingQuestionPartImportHandler(IStringLocalizer<OrderingQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Items", S["The items in their right order, separated by |, such as: Plan|Do|Check|Act."], (part, value) => part.Items = List(value).Select(text => new ChoiceOption
            {
                Id = UniqueId.GenerateId(),
                Text = text,
                Correct = true,
            }).ToList(), part => Join(part.Items.Select(item => item.Text))),
            new("PartialCredit", S["Whether each item in its right place earns its share of the points: yes or no."], (part, value) => part.PartialCredit = Boolean(value), part => part.PartialCredit ? "yes" : "no"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<OrderingQuestionPart>> Columns { get; }
}

/// <summary>
/// The text of a fill in the blanks question, with each blank written as <c>[[answer|other answer]]</c>.
/// </summary>
internal sealed class FillInBlanksQuestionPartImportHandler : QuestionColumnsImportHandler<FillInBlanksQuestionPart>
{
    public FillInBlanksQuestionPartImportHandler(IStringLocalizer<FillInBlanksQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Text", S["The text with each blank written as [[answer]], or [[answer|other answer]] for several accepted answers, such as: Water boils at [[100]] degrees."], (part, value) => part.Text = value, part => part.Text),
            new("CaseSensitive", S["Whether capital letters must match: yes or no."], (part, value) => part.CaseSensitive = Boolean(value), part => part.CaseSensitive ? "yes" : "no"),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<FillInBlanksQuestionPart>> Columns { get; }
}

/// <summary>
/// The word limits and grader guidance of an essay question.
/// </summary>
internal sealed class EssayQuestionPartImportHandler : QuestionColumnsImportHandler<EssayQuestionPart>
{
    public EssayQuestionPartImportHandler(IStringLocalizer<EssayQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("MinWords", S["The fewest words an answer may have; 0 for no limit."], (part, value) => part.MinWords = int.TryParse(value, out var words) ? Math.Max(words, 0) : part.MinWords, part => part.MinWords.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("MaxWords", S["The most words an answer may have; 0 for no limit."], (part, value) => part.MaxWords = int.TryParse(value, out var words) ? Math.Max(words, 0) : part.MaxWords, part => part.MaxWords.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("GraderGuidance", S["What a grader looks for; never shown to learners."], (part, value) => part.GraderGuidance = value, part => part.GraderGuidance),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<EssayQuestionPart>> Columns { get; }
}
