using System.Net;
using CrestApps.OrchardCore.Learning.Assessments;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Learning.Coursework.Models;
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
    /// Gets the content type of a drafted question type.
    /// </summary>
    /// <param name="type">The drafted type.</param>
    /// <returns>The content type, or <see langword="null"/>.</returns>
    public static string QuestionContentType(string type)
        => type switch
        {
            "SingleChoice" => AssessmentsConstants.ContentTypes.SingleChoiceQuestion,
            "MultipleChoice" => AssessmentsConstants.ContentTypes.MultipleChoiceQuestion,
            "TrueFalse" => AssessmentsConstants.ContentTypes.TrueFalseQuestion,
            "ShortText" => AssessmentsConstants.ContentTypes.ShortTextQuestion,
            "Essay" => AssessmentsConstants.ContentTypes.EssayQuestion,
            _ => null,
        };

    /// <summary>
    /// Writes a drafted question into a new question content item.
    /// </summary>
    /// <param name="item">The question content item.</param>
    /// <param name="question">The drafted question.</param>
    public static void ApplyQuestion(ContentItem item, DraftQuestion question)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(question);

        var title = ShortTitle(question.Prompt);

        item.DisplayText = title;
        item.Alter<TitlePart>(part => part.Title = title);
        item.Alter<QuestionPart>(part =>
        {
            part.Prompt = Paragraphs(question.Prompt);
            part.Points = question.Points;
            part.GeneralFeedback = question.Type == "Essay" ? null : question.Feedback;
        });

        switch (question.Type)
        {
            case "SingleChoice":
            case "MultipleChoice":
                item.Alter<ChoiceQuestionPart>(part =>
                {
                    part.ShuffleOptions = true;
                    part.Options = question.Options
                        .Select((option, index) => new ChoiceOption { Id = "o" + (index + 1), Text = option.Text, Correct = option.Correct })
                        .ToList();
                });
                break;
            case "TrueFalse":
                item.Alter<TrueFalseQuestionPart>(part => part.Answer = question.Answer);
                break;
            case "ShortText":
                item.Alter<ShortTextQuestionPart>(part => part.AcceptedAnswers = question.AcceptedAnswers.ToList());
                break;
            case "Essay":
                item.Alter<EssayQuestionPart>(part => part.GraderGuidance = question.Feedback);
                break;
        }
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
    public static string ShortTitle(string text)
    {
        var title = (text ?? string.Empty).Replace('\n', ' ').Trim();

        return title.Length <= 80 ? title : title[..77].TrimEnd() + "...";
    }
}
