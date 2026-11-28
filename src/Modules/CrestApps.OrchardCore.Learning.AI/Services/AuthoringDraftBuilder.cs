using System.Globalization;
using System.Net;
using CrestApps.OrchardCore.Learning.Coursework.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.Title.Models;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// Writes drafted questions and rubrics into content items. The text the model wrote is HTML-encoded, never trusted as
/// markup.
/// </summary>
internal static class AuthoringDraftBuilder
{
    /// <summary>
    /// Builds a survey question from a drafted question. The drafted type names a question type of Surveys: a choice,
    /// several choices, true or false, a short text or a written answer.
    /// </summary>
    /// <param name="question">The drafted question.</param>
    /// <returns>The survey question, or <see langword="null"/> for a type no survey question type matches.</returns>
    public static SurveyQuestion BuildQuestion(DraftQuestion question)
    {
        ArgumentNullException.ThrowIfNull(question);

        var controlType = question.Type switch
        {
            "SingleChoice" => SurveyControlNames.SingleChoice,
            "MultipleChoice" => SurveyControlNames.MultipleChoice,
            "TrueFalse" => SurveyControlNames.Boolean,
            "ShortText" => SurveyControlNames.ShortText,
            "Essay" => SurveyControlNames.LongText,
            _ => null,
        };

        if (controlType is null)
        {
            return null;
        }

        var built = new SurveyQuestion
        {
            Id = IdGenerator.GenerateId(),
            ControlType = controlType,
            Title = ShortText(question.Prompt),
            Description = question.Prompt?.Contains('\n') == true ? Paragraphs(question.Prompt) : null,
            Required = true,
        };

        built.Scoring.Points = question.Points;

        switch (question.Type)
        {
            case "SingleChoice":
            case "MultipleChoice":
                built.ShuffleChoices = true;
                built.Choices = question.Options
                    .Select((option, index) => new SurveyChoice { Id = IdGenerator.GenerateId(), Value = (index + 1).ToString(CultureInfo.InvariantCulture), Text = option.Text })
                    .ToList();
                built.Scoring.CorrectChoiceIds = built.Choices
                    .Where((choice, index) => question.Options[index].Correct)
                    .Select(choice => choice.Id)
                    .ToList();
                built.Scoring.PartialCredit = question.Type == "MultipleChoice";
                built.Scoring.GeneralFeedback = question.Feedback;
                break;
            case "TrueFalse":
                built.Settings["Display"] = "TrueFalse";
                built.Scoring.AcceptedAnswers = [question.Answer ? "true" : "false"];
                built.Scoring.Key["answer"] = question.Answer;
                built.Scoring.GeneralFeedback = question.Feedback;
                break;
            case "ShortText":
                built.Scoring.AcceptedAnswers = question.AcceptedAnswers.ToList();
                built.Scoring.GeneralFeedback = question.Feedback;
                break;
            case "Essay":
                built.Scoring.GraderGuidance = question.Feedback;
                break;
        }

        return built;
    }

    /// <summary>
    /// Writes a drafted rubric into a new rubric content item.
    /// </summary>
    /// <param name="item">The rubric content item.</param>
    /// <param name="rubric">The drafted rubric.</param>
    public static void ApplyRubric(ContentItem item, DraftRubric rubric)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(rubric);

        item.DisplayText = rubric.Title;
        item.Alter<TitlePart>(part => part.Title = rubric.Title);
        item.Alter<RubricPart>(part => part.Criteria = rubric.Criteria
            .Select((criterion, index) => new RubricCriterion
            {
                Id = "c" + (index + 1),
                Title = criterion.Title,
                Description = criterion.Description,
                Levels = criterion.Levels
                    .Select((level, levelIndex) => new RubricLevel { Id = $"c{index + 1}l{levelIndex + 1}", Title = level.Title, Points = level.Points })
                    .ToList(),
            })
            .ToList());
    }

    /// <summary>
    /// Writes plain text as one HTML paragraph per line.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The HTML.</returns>
    public static string Paragraphs(string text)
        => string.IsNullOrWhiteSpace(text)
            ? string.Empty
            : string.Concat(text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => "<p>" + WebUtility.HtmlEncode(line) + "</p>"));

    /// <summary>
    /// Gets a short title from a longer text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>At most 80 characters of the text.</returns>
    /// <summary>
    /// Gets a question title from a prompt: its text on one line, cut to 500 characters.
    /// </summary>
    /// <param name="text">The prompt.</param>
    /// <returns>The title.</returns>
    public static string ShortText(string text)
    {
        var title = (text ?? string.Empty).Replace('\n', ' ').Trim();

        return title.Length <= 500 ? title : title.Substring(0, 497).TrimEnd() + "...";
    }

    /// <summary>
    /// Gets a short title from a text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The title.</returns>
    public static string ShortTitle(string text)
    {
        var title = (text ?? string.Empty).Replace('\n', ' ').Trim();

        return title.Length <= 80 ? title : title[..77].TrimEnd() + "...";
    }
}
