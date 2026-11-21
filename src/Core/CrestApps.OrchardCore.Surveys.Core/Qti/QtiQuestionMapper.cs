using System.Globalization;
using System.Net;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;

namespace CrestApps.OrchardCore.Surveys.Core.Qti;

/// <summary>
/// Turns a question read from a QTI item into a bank question of a Surveys question type.
/// </summary>
/// <remarks>
/// Settings of the blank types are written under the keys <c>Text</c> (with <c>[[answer|other answer]]</c> blanks),
/// <c>Distractors</c>, <c>Display</c> and <c>CaseSensitive</c>; a true or false key under
/// <c>Scoring.AcceptedAnswers</c> and <c>Scoring.Key["answer"]</c>.
/// </remarks>
public static partial class QtiQuestionMapper
{
    /// <summary>
    /// The prefix of the source identifier of an imported item.
    /// </summary>
    public const string SourcePrefix = "qti:";

    /// <summary>
    /// Maps a QTI item to a bank question.
    /// </summary>
    /// <param name="item">The item, which must be importable.</param>
    /// <returns>The bank question, not saved; <see langword="null"/> when the item was refused or its kind is unknown.</returns>
    public static BankQuestion Map(QtiItem item)
    {
        if (item?.Question is not { } qti || !item.Imported)
        {
            return null;
        }

        var question = new SurveyQuestion
        {
            Id = IdGenerator.GenerateId(),
        };

        question.Name = "q_" + question.Id.ToLowerInvariant();
        question.Title = Title(qti.Prompt, item.Title);
        question.Description = HasMarkup(qti.Prompt) ? qti.Prompt : null;
        question.Scoring.Points = qti.Points;
        question.Scoring.GeneralFeedback = qti.GeneralFeedback;
        question.Scoring.PartialCredit = qti.PartialCredit;

        switch (qti.QuestionType)
        {
            case QtiQuestionTypes.Description:
                question.ControlType = SurveyControlNames.TextBlock;
                question.Settings["Html"] = qti.Prompt;
                question.Scoring.Points = 0;
                break;
            case QtiQuestionTypes.SingleChoice:
            case QtiQuestionTypes.MultipleChoice:
                question.ControlType = qti.QuestionType == QtiQuestionTypes.MultipleChoice ? SurveyControlNames.MultipleChoice : SurveyControlNames.SingleChoice;
                question.ShuffleChoices = qti.Shuffle;
                AddOptions(question, qti, markCorrect: true);
                break;
            case QtiQuestionTypes.TrueFalse:
                question.ControlType = SurveyControlNames.Boolean;
                question.Settings["Display"] = "TrueFalse";
                question.Scoring.AcceptedAnswers = [qti.TrueFalseAnswer ? "true" : "false"];
                question.Scoring.Key["answer"] = qti.TrueFalseAnswer;
                break;
            case QtiQuestionTypes.ShortText:
                question.ControlType = SurveyControlNames.ShortText;
                question.Scoring.AcceptedAnswers = qti.AcceptedAnswers.ToList();
                question.Scoring.CaseSensitive = qti.CaseSensitive;
                break;
            case QtiQuestionTypes.Numeric:
                question.ControlType = SurveyControlNames.Number;
                question.Scoring.NumberAnswer = qti.NumericAnswer;
                question.Scoring.Tolerance = qti.Tolerance;
                break;
            case QtiQuestionTypes.Essay:
                question.ControlType = SurveyControlNames.LongText;
                question.Scoring.GraderGuidance = qti.GraderGuidance;
                break;
            case QtiQuestionTypes.Matching:
                question.ControlType = SurveyControlNames.Matching;
                question.ShuffleChoices = true;
                AddPairs(question, qti);
                break;
            case QtiQuestionTypes.Ordering:
                question.ControlType = SurveyControlNames.Ranking;
                question.ShuffleChoices = true;

                // The options come in the right order: a ranking is scored against the order of its choices.
                AddOptions(question, qti, markCorrect: false);
                break;
            case QtiQuestionTypes.FillInBlanks:
                question.ControlType = SurveyControlNames.FillInBlanks;
                question.Settings["Text"] = qti.BlankText;
                question.Settings["CaseSensitive"] = qti.CaseSensitive;
                question.Scoring.CaseSensitive = qti.CaseSensitive;
                break;
            case QtiQuestionTypes.DragIntoText:
            case QtiQuestionTypes.GapSelect:
                question.ControlType = SurveyControlNames.ChoiceBlanks;
                question.Settings["Text"] = qti.BlankText;
                question.Settings["Distractors"] = new JsonArray(qti.Distractors.Select(text => (JsonNode)JsonValue.Create(text)).ToArray());
                question.Settings["Display"] = qti.QuestionType == QtiQuestionTypes.GapSelect ? "GapSelect" : "DragIntoText";
                break;
            default:
                return null;
        }

        return new BankQuestion
        {
            Question = question,
            SourceIdentifier = string.IsNullOrEmpty(item.Identifier) ? null : SourcePrefix + item.Identifier,
        };
    }

    private static void AddOptions(SurveyQuestion question, QtiQuestion qti, bool markCorrect)
    {
        var position = 0;

        foreach (var option in qti.Options)
        {
            position++;

            var choice = new SurveyChoice
            {
                Id = IdGenerator.GenerateId(),
                Value = string.IsNullOrEmpty(option.Identifier) ? position.ToString(CultureInfo.InvariantCulture) : option.Identifier,
                Text = option.Text,
            };

            question.Choices.Add(choice);

            if (markCorrect && option.Correct)
            {
                question.Scoring.CorrectChoiceIds.Add(choice.Id);
            }

            if (!string.IsNullOrEmpty(option.Feedback))
            {
                question.Scoring.ChoiceFeedback[choice.Id] = option.Feedback;
            }
        }
    }

    private static void AddPairs(SurveyQuestion question, QtiQuestion qti)
    {
        foreach (var (prompt, answer) in qti.Pairs)
        {
            var row = question.Rows.FirstOrDefault(item => string.Equals(item.Text, prompt, StringComparison.Ordinal));

            if (row is null)
            {
                row = new SurveySubQuestion
                {
                    Id = IdGenerator.GenerateId(),
                    Name = "r" + (question.Rows.Count + 1).ToString(CultureInfo.InvariantCulture),
                    Text = prompt,
                };

                question.Rows.Add(row);
            }

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

        // Distractors are answers no prompt matches.
        foreach (var distractor in qti.Distractors.Where(text => !question.Choices.Any(choice => string.Equals(choice.Text, text, StringComparison.Ordinal))))
        {
            question.Choices.Add(new SurveyChoice
            {
                Id = IdGenerator.GenerateId(),
                Value = (question.Choices.Count + 1).ToString(CultureInfo.InvariantCulture),
                Text = distractor,
            });
        }
    }

    private static string Title(string prompt, string fallback)
    {
        var text = string.IsNullOrEmpty(prompt) ? null : WebUtility.HtmlDecode(Spaces().Replace(Tags().Replace(prompt, " "), " ")).Trim();

        if (string.IsNullOrEmpty(text))
        {
            text = fallback?.Trim() ?? string.Empty;
        }

        return text.Length > 1000 ? text.Substring(0, 1000) : text;
    }

    private static bool HasMarkup(string prompt)
        => !string.IsNullOrEmpty(prompt) && Tags().IsMatch(prompt);

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
