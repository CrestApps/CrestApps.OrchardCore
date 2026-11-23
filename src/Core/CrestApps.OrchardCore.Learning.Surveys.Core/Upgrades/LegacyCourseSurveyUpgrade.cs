using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Surveys.Models;
using CrestApps.OrchardCore.Surveys;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Surveys.Core.Upgrades;

/// <summary>
/// Upgrades the course surveys of the first Learning release, whose questions lived in a <c>SurveyPart</c> of the item,
/// to course surveys that own a survey of the Surveys module (<see cref="CourseSurveyPart"/>).
/// </summary>
public static class LegacyCourseSurveyUpgrade
{
    /// <summary>
    /// The name of the part the first release used. The Surveys module now uses this name for its embed part.
    /// </summary>
    public const string LegacyPartName = "SurveyPart";

    // The question types of the first release, by their number and name.
    private static readonly string[] _legacyTypes = ["SingleChoice", "MultipleChoice", "Rating", "Text"];

    /// <summary>
    /// Reads the legacy part of an item without changing the item.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <returns>The legacy part, or <see langword="null"/> when the item has none.</returns>
    public static JsonObject ReadLegacyPart(ContentItem item)
        => ((JsonObject)item?.Content)?[LegacyPartName] as JsonObject;

    /// <summary>
    /// Moves the legacy part of an item to a <see cref="CourseSurveyPart"/>, and returns what it held.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <returns>The legacy part, or <see langword="null"/> when the item has none.</returns>
    public static JsonObject TakeLegacyPart(ContentItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var content = (JsonObject)item.Content;

        if (content?[LegacyPartName] is not JsonObject legacy)
        {
            return null;
        }

        content.Remove(LegacyPartName);

        if (content[nameof(CourseSurveyPart)] is not JsonObject)
        {
            content[nameof(CourseSurveyPart)] = new JsonObject();
        }

        return legacy;
    }

    /// <summary>
    /// Sets the survey a course survey item owns.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <param name="surveyId">The survey.</param>
    public static void SetSurveyId(ContentItem item, string surveyId)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.Alter<CourseSurveyPart>(part => part.SurveyId = surveyId);
    }

    /// <summary>
    /// Builds the survey definition of a legacy part's questions: single and multiple choice, rating (1 to 5) and text.
    /// </summary>
    /// <param name="legacy">The legacy part.</param>
    /// <param name="title">The title respondents see.</param>
    /// <returns>The definition, or <see langword="null"/> when the part has no questions.</returns>
    public static SurveyDefinition ToDefinition(JsonObject legacy, string title)
    {
        if (legacy?["Questions"] is not JsonArray questions || questions.Count == 0)
        {
            return null;
        }

        var page = new SurveyPage { Id = "p1", Name = "page_1", Title = title };
        var number = 0;

        foreach (var node in questions)
        {
            if (node is not JsonObject source)
            {
                continue;
            }

            var text = source["Text"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            number++;
            var name = "q" + number.ToString(CultureInfo.InvariantCulture);
            var question = new SurveyQuestion
            {
                Id = name,
                Name = name,
                Title = text.Trim(),
                Required = source["Required"] is JsonValue required && required.TryGetValue<bool>(out var isRequired) && isRequired,
                ControlType = TypeOf(source["Type"]) switch
                {
                    "MultipleChoice" => SurveyControlNames.MultipleChoice,
                    "Rating" => SurveyControlNames.Rating,
                    "Text" => SurveyControlNames.LongText,
                    _ => SurveyControlNames.SingleChoice,
                },
            };

            if (question.ControlType is SurveyControlNames.SingleChoice or SurveyControlNames.MultipleChoice && source["Options"] is JsonArray options)
            {
                var choice = 0;

                foreach (var option in options)
                {
                    var optionText = option?.GetValue<string>();

                    if (string.IsNullOrWhiteSpace(optionText))
                    {
                        continue;
                    }

                    choice++;
                    var value = choice.ToString(CultureInfo.InvariantCulture);
                    question.Choices.Add(new SurveyChoice { Id = name + "_" + value, Value = value, Text = optionText.Trim() });
                }
            }

            page.Questions.Add(question);
        }

        if (page.Questions.Count == 0)
        {
            return null;
        }

        return new SurveyDefinition
        {
            Title = title,
            Pages = [page],
        };
    }

    /// <summary>
    /// Carries the legacy part's settings to the survey: anonymous answers and the close date.
    /// </summary>
    /// <param name="legacy">The legacy part.</param>
    /// <param name="survey">The survey.</param>
    public static void ApplySettings(JsonObject legacy, Survey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (legacy is null)
        {
            return;
        }

        survey.Access ??= new SurveyAccessSettings();

        if (legacy["Anonymous"] is JsonValue anonymous && anonymous.TryGetValue<bool>(out var isAnonymous) && isAnonymous)
        {
            survey.Access.Anonymity = AnonymityMode.Anonymous;
        }

        if (legacy["CloseUtc"] is JsonValue close
            && (close.TryGetValue<DateTime>(out var closeUtc)
                || (close.TryGetValue<string>(out var closeText) && DateTime.TryParse(closeText, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out closeUtc))))
        {
            survey.Access.ClosesUtc = DateTime.SpecifyKind(closeUtc, DateTimeKind.Utc);
        }
    }

    private static string TypeOf(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return _legacyTypes[0];
        }

        if (value.TryGetValue<int>(out var number))
        {
            return number >= 0 && number < _legacyTypes.Length ? _legacyTypes[number] : _legacyTypes[0];
        }

        return value.TryGetValue<string>(out var name) && _legacyTypes.Contains(name, StringComparer.OrdinalIgnoreCase)
            ? _legacyTypes.First(type => string.Equals(type, name, StringComparison.OrdinalIgnoreCase))
            : _legacyTypes[0];
    }
}
