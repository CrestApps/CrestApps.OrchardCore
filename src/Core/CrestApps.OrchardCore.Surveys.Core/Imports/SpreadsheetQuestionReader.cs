using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using CrestApps.OrchardCore.Tabular;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.Surveys.Core.Imports;

/// <summary>
/// Turns the rows of a question spreadsheet into bank questions. The columns are <c>Id</c> (the source identifier),
/// <c>Type</c> (a question type or preset name), <c>Question</c>, <c>Choices</c> (separated by <c>|</c>; for types with
/// rows, <c>prompt=answer</c> pairs), <c>Correct</c> (choice texts or values separated by <c>|</c>, or the accepted text
/// or number answers), <c>Points</c>, <c>Tags</c> (separated by commas), <c>Difficulty</c> (1 to 5) and <c>Feedback</c>.
/// What a row means for a type comes from the type's capabilities, never from its name.
/// </summary>
public sealed class SpreadsheetQuestionReader
{
    /// <summary>
    /// The column of the source identifier.
    /// </summary>
    public const string IdColumn = "Id";

    /// <summary>
    /// The column of the question type.
    /// </summary>
    public const string TypeColumn = "Type";

    /// <summary>
    /// The column of the question text.
    /// </summary>
    public const string QuestionColumn = "Question";

    /// <summary>
    /// The column of the choices.
    /// </summary>
    public const string ChoicesColumn = "Choices";

    /// <summary>
    /// The column of the right answers.
    /// </summary>
    public const string CorrectColumn = "Correct";

    /// <summary>
    /// The column of the points.
    /// </summary>
    public const string PointsColumn = "Points";

    /// <summary>
    /// The column of the tags.
    /// </summary>
    public const string TagsColumn = "Tags";

    /// <summary>
    /// The column of the difficulty.
    /// </summary>
    public const string DifficultyColumn = "Difficulty";

    /// <summary>
    /// The column of the general feedback.
    /// </summary>
    public const string FeedbackColumn = "Feedback";

    /// <summary>
    /// The most rows read from one file.
    /// </summary>
    public const int MaxRows = 5_000;

    private static readonly char[] _listSeparator = ['|'];
    private static readonly char[] _tagSeparator = [',', ';'];

    private readonly ISurveyControlRegistry _controls;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpreadsheetQuestionReader"/> class.
    /// </summary>
    /// <param name="controls">The question type registry.</param>
    /// <param name="stringLocalizer">The localizer.</param>
    public SpreadsheetQuestionReader(
        ISurveyControlRegistry controls,
        IStringLocalizer<SpreadsheetQuestionReader> stringLocalizer)
    {
        _controls = controls;
        S = stringLocalizer;
    }

    /// <summary>
    /// Reads questions from the rows of a sheet.
    /// </summary>
    /// <param name="headers">The column names of the sheet.</param>
    /// <param name="rows">The rows.</param>
    /// <returns>The questions read and the problems found.</returns>
    public SurveyImportResult Read(IReadOnlyList<string> headers, IReadOnlyList<CsvRow> rows)
    {
        var result = new SurveyImportResult();

        if (headers is null || !headers.Any(header => string.Equals(header?.Trim(), TypeColumn, StringComparison.OrdinalIgnoreCase))
            || !headers.Any(header => string.Equals(header?.Trim(), QuestionColumn, StringComparison.OrdinalIgnoreCase)))
        {
            result.Errors.Add(S["The file needs a Type column and a Question column."]);

            return result;
        }

        if (rows.Count > MaxRows)
        {
            result.Errors.Add(S["The file has more than {0} rows.", MaxRows]);

            return result;
        }

        foreach (var row in rows)
        {
            if (IsBlank(row))
            {
                continue;
            }

            var errors = new List<string>();
            var question = ReadRow(row, errors);

            if (errors.Count > 0)
            {
                foreach (var error in errors)
                {
                    result.Errors.Add(S["Row {0}: {1}", row.Number, error]);
                }

                continue;
            }

            result.Questions.Add(question);
        }

        return result;
    }

    private BankQuestion ReadRow(CsvRow row, List<string> errors)
    {
        var typeName = row[TypeColumn]?.Trim();
        var text = row[QuestionColumn]?.Trim();

        if (string.IsNullOrEmpty(text))
        {
            errors.Add(S["The question text is empty."]);
        }

        var preset = _controls.GetPreset(typeName);
        var descriptor = _controls.GetControl(preset?.BaseControl ?? typeName);

        if (descriptor is null)
        {
            errors.Add(S["The question type '{0}' is not available.", typeName ?? string.Empty]);

            return null;
        }

        var question = new SurveyQuestion
        {
            Id = IdGenerator.GenerateId(),
            ControlType = descriptor.Name,
            Title = text,
        };

        question.Name = "q_" + question.Id.ToLowerInvariant();
        _controls.GetHandler(descriptor.Name)?.Initialize(question);
        preset?.Initialize?.Invoke(question);

        var bankQuestion = new BankQuestion
        {
            Question = question,
            SourceIdentifier = NullIfEmpty(row[IdColumn]),
            Tags = Split(row[TagsColumn], _tagSeparator),
        };

        var difficulty = NullIfEmpty(row[DifficultyColumn]);

        if (difficulty is not null)
        {
            if (int.TryParse(difficulty, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) && level is >= 1 and <= 5)
            {
                bankQuestion.Difficulty = level;
            }
            else
            {
                errors.Add(S["The difficulty must be a whole number from 1 to 5."]);
            }
        }

        var points = NullIfEmpty(row[PointsColumn]);

        if (points is not null)
        {
            if (decimal.TryParse(points, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) && value >= 0)
            {
                question.Scoring.Points = value;
            }
            else
            {
                errors.Add(S["The points must be a number of zero or more."]);
            }
        }

        question.Scoring.GeneralFeedback = NullIfEmpty(row[FeedbackColumn]);

        var choices = Split(row[ChoicesColumn], _listSeparator);
        var correct = Split(row[CorrectColumn], _listSeparator);

        if (descriptor.Has(SurveyControlCapabilities.UsesRows) && descriptor.Has(SurveyControlCapabilities.UsesChoices))
        {
            ReadPairs(question, choices, errors);
        }
        else if (descriptor.Has(SurveyControlCapabilities.UsesChoices))
        {
            ReadChoices(question, choices, correct, errors);
        }
        else if (correct.Count > 0)
        {
            ReadAnswers(question, correct, errors);
        }

        return bankQuestion;
    }

    private void ReadChoices(SurveyQuestion question, List<string> choices, List<string> correct, List<string> errors)
    {
        if (choices.Count == 0)
        {
            errors.Add(S["The question needs choices."]);

            return;
        }

        question.Choices.Clear();

        for (var i = 0; i < choices.Count; i++)
        {
            question.Choices.Add(new SurveyChoice
            {
                Id = IdGenerator.GenerateId(),
                Value = (i + 1).ToString(CultureInfo.InvariantCulture),
                Text = choices[i],
            });
        }

        foreach (var answer in correct)
        {
            var match = question.Choices.FirstOrDefault(choice => string.Equals(choice.Text, answer, StringComparison.OrdinalIgnoreCase))
                ?? question.Choices.FirstOrDefault(choice => string.Equals(choice.Value, answer, StringComparison.OrdinalIgnoreCase));

            if (match is null)
            {
                errors.Add(S["The right answer '{0}' is not one of the choices.", answer]);

                continue;
            }

            if (!question.Scoring.CorrectChoiceIds.Contains(match.Id))
            {
                question.Scoring.CorrectChoiceIds.Add(match.Id);
            }
        }
    }

    private void ReadPairs(SurveyQuestion question, List<string> pairs, List<string> errors)
    {
        if (pairs.Count == 0)
        {
            errors.Add(S["The question needs pairs written as prompt=answer."]);

            return;
        }

        question.Rows.Clear();
        question.Choices.Clear();

        foreach (var pair in pairs)
        {
            var separator = pair.IndexOf('=', StringComparison.Ordinal);

            if (separator <= 0 || separator == pair.Length - 1)
            {
                errors.Add(S["'{0}' is not written as prompt=answer.", pair]);

                continue;
            }

            var prompt = pair.Substring(0, separator).Trim();
            var answer = pair.Substring(separator + 1).Trim();
            var row = new SurveySubQuestion
            {
                Id = IdGenerator.GenerateId(),
                Name = "r" + (question.Rows.Count + 1).ToString(CultureInfo.InvariantCulture),
                Text = prompt,
            };

            question.Rows.Add(row);

            var choice = question.Choices.FirstOrDefault(item => string.Equals(item.Text, answer, StringComparison.Ordinal));

            if (choice is null)
            {
                choice = new SurveyChoice
                {
                    Id = IdGenerator.GenerateId(),
                    Value = (question.Choices.Count + 1).ToString(CultureInfo.InvariantCulture),
                    Text = answer,
                };

                question.Choices.Add(choice);
            }

            question.Scoring.Pairs[row.Id] = choice.Id;
        }
    }

    private void ReadAnswers(SurveyQuestion question, List<string> correct, List<string> errors)
    {
        var type = _controls.GetHandler(question.ControlType)?.GetFormulaType(question, null) ?? DataFieldType.Text;

        if (type is DataFieldType.Integer or DataFieldType.Decimal)
        {
            if (decimal.TryParse(correct[0], NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
            {
                question.Scoring.NumberAnswer = number;
            }
            else
            {
                errors.Add(S["The right answer '{0}' is not a number.", correct[0]]);
            }

            return;
        }

        if (type == DataFieldType.Boolean)
        {
            var value = correct[0].Trim().ToLowerInvariant();
            bool? answer = value switch
            {
                "true" or "yes" or "1" => true,
                "false" or "no" or "0" => false,
                _ => null,
            };

            if (answer is null)
            {
                errors.Add(S["The right answer '{0}' is not true or false.", correct[0]]);

                return;
            }

            question.Scoring.AcceptedAnswers = [answer.Value ? "true" : "false"];
            question.Scoring.Key["answer"] = answer.Value;

            return;
        }

        question.Scoring.AcceptedAnswers = correct.ToList();
    }

    private static bool IsBlank(CsvRow row)
        => string.IsNullOrWhiteSpace(row[TypeColumn]) && string.IsNullOrWhiteSpace(row[QuestionColumn]);

    private static List<string> Split(string value, char[] separators)
        => string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static string NullIfEmpty(string value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
