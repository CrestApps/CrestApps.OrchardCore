using CrestApps.OrchardCore.Learning.Assessments.Models;
using Microsoft.Extensions.Localization;
using CrestApps.Core;

namespace CrestApps.OrchardCore.Learning.Assessments.ContentTransfer;

/// <summary>
/// The prompt, points, difficulty, tags and general feedback of every question.
/// </summary>
internal sealed class QuestionPartImportHandler : QuestionColumnsImportHandler<QuestionPart>
{
    public QuestionPartImportHandler(IStringLocalizer<QuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Prompt", S["The question, as text or HTML."], (part, value) => part.Prompt = value, part => part.Prompt),
            new("Points", S["The points of the question, a number."], (part, value) => part.Points = Decimal(value) ?? part.Points, part => Text(part.Points)),
            new("Difficulty", S["The difficulty from 1 (easy) to 5 (hard)."], (part, value) => part.Difficulty = int.TryParse(value, out var difficulty) ? Math.Clamp(difficulty, 1, 5) : part.Difficulty, part => part.Difficulty.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            new("Tags", S["Tags separated by |, used by random draws."], (part, value) => part.Tags = List(value), part => string.Join(ListSeparator, part.Tags)),
            new("GeneralFeedback", S["The feedback shown on review."], (part, value) => part.GeneralFeedback = value, part => part.GeneralFeedback),
        ];
    }

    protected override IReadOnlyList<QuestionColumn<QuestionPart>> Columns { get; }
}

/// <summary>
/// The options of a single or multiple choice question: separated by |, the right ones starting with *.
/// </summary>
internal sealed class ChoiceQuestionPartImportHandler : QuestionColumnsImportHandler<ChoiceQuestionPart>
{
    public ChoiceQuestionPartImportHandler(IStringLocalizer<ChoiceQuestionPartImportHandler> S)
    {
        Columns =
        [
            new("Options", S["The options separated by |; start each right option with *, such as: Foam|*CO2|Water."], (part, value) => part.Options = List(value).Select(text => new ChoiceOption
            {
                Id = UniqueId.GenerateId(),
                Text = text.TrimStart('*').Trim(),
                Correct = text.StartsWith('*'),
            }).ToList(), part => string.Join(ListSeparator, part.Options.Select(option => (option.Correct ? "*" : string.Empty) + option.Text))),
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
            new("AcceptedAnswers", S["The accepted answers separated by |."], (part, value) => part.AcceptedAnswers = List(value), part => string.Join(ListSeparator, part.AcceptedAnswers)),
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
