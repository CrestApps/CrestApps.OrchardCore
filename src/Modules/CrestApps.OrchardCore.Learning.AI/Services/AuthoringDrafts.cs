using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Learning.AI.Services;

/// <summary>
/// A drafted course outline.
/// </summary>
public sealed class DraftOutline
{
    /// <summary>
    /// Gets or sets the course title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the course summary.
    /// </summary>
    public string Summary { get; set; }

    /// <summary>
    /// Gets the sections.
    /// </summary>
    public IList<DraftSection> Sections { get; } = [];
}

/// <summary>
/// A drafted section of an outline.
/// </summary>
public sealed class DraftSection
{
    /// <summary>
    /// Gets or sets the section title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets the lessons.
    /// </summary>
    public IList<DraftLesson> Lessons { get; } = [];
}

/// <summary>
/// A drafted lesson of an outline.
/// </summary>
public sealed class DraftLesson
{
    /// <summary>
    /// Gets or sets the lesson title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets what the lesson covers.
    /// </summary>
    public string Summary { get; set; }
}

/// <summary>
/// A drafted question.
/// </summary>
public sealed class DraftQuestion
{
    /// <summary>
    /// Gets or sets the question type: SingleChoice, MultipleChoice, TrueFalse, ShortText or Essay.
    /// </summary>
    public string Type { get; set; }

    /// <summary>
    /// Gets or sets the prompt.
    /// </summary>
    public string Prompt { get; set; }

    /// <summary>
    /// Gets the options of a choice question.
    /// </summary>
    public IList<DraftOption> Options { get; } = [];

    /// <summary>
    /// Gets or sets the answer of a true or false question.
    /// </summary>
    public bool Answer { get; set; }

    /// <summary>
    /// Gets the accepted answers of a short text question.
    /// </summary>
    public IList<string> AcceptedAnswers { get; } = [];

    /// <summary>
    /// Gets or sets the general feedback, or the grader guidance of an essay.
    /// </summary>
    public string Feedback { get; set; }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    public decimal Points { get; set; } = 1;
}

/// <summary>
/// An option of a drafted choice question.
/// </summary>
public sealed class DraftOption
{
    /// <summary>
    /// Gets or sets the option text.
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Gets or sets whether the option is right.
    /// </summary>
    public bool Correct { get; set; }
}

/// <summary>
/// A drafted rubric.
/// </summary>
public sealed class DraftRubric
{
    /// <summary>
    /// Gets or sets the rubric title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets the criteria.
    /// </summary>
    public IList<DraftCriterion> Criteria { get; } = [];
}

/// <summary>
/// A criterion of a drafted rubric.
/// </summary>
public sealed class DraftCriterion
{
    /// <summary>
    /// Gets or sets the criterion title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the criterion description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets the levels, from the lowest to the highest.
    /// </summary>
    public IList<DraftLevel> Levels { get; } = [];
}

/// <summary>
/// A level of a drafted rubric criterion.
/// </summary>
public sealed class DraftLevel
{
    /// <summary>
    /// Gets or sets the level title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the points.
    /// </summary>
    public decimal Points { get; set; }
}

/// <summary>
/// A drafted translation of a lesson.
/// </summary>
public sealed class DraftTranslation
{
    /// <summary>
    /// Gets or sets the translated title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the translated body, as HTML.
    /// </summary>
    public string Body { get; set; }
}

/// <summary>
/// Reads what the model drafted. The model is asked for JSON; anything that is not usable JSON, or is outside the limits,
/// is left out rather than guessed, and a draft with nothing usable is <see langword="null"/>.
/// </summary>
internal static class AuthoringDraftParser
{
    public const int MaxSections = 12;
    public const int MaxLessonsPerSection = 10;
    public const int MaxQuestions = 20;
    public const int MaxOptions = 8;
    public const int MaxCriteria = 10;
    public const int MaxLevels = 6;
    public const int MaxTitleLength = 200;
    public const int MaxTextLength = 4000;
    public const int MaxBodyLength = 100_000;

    public static readonly string[] QuestionTypes = ["SingleChoice", "MultipleChoice", "TrueFalse", "ShortText", "Essay"];

    public static DraftOutline ParseOutline(string text)
    {
        var root = ReadObject(text);
        var title = String(root?["title"], MaxTitleLength);

        if (title is null || root["sections"] is not JsonArray sections)
        {
            return null;
        }

        var outline = new DraftOutline
        {
            Title = title,
            Summary = String(root["summary"], MaxTextLength),
        };

        foreach (var sectionNode in sections.OfType<JsonObject>())
        {
            var sectionTitle = String(sectionNode["title"], MaxTitleLength);

            if (sectionTitle is null)
            {
                continue;
            }

            var section = new DraftSection { Title = sectionTitle };

            if (sectionNode["lessons"] is JsonArray lessons)
            {
                foreach (var lessonNode in lessons.OfType<JsonObject>())
                {
                    var lessonTitle = String(lessonNode["title"], MaxTitleLength);

                    if (lessonTitle is not null && section.Lessons.Count < MaxLessonsPerSection)
                    {
                        section.Lessons.Add(new DraftLesson { Title = lessonTitle, Summary = String(lessonNode["summary"], MaxTextLength) });
                    }
                }
            }

            if (section.Lessons.Count > 0)
            {
                outline.Sections.Add(section);
            }

            if (outline.Sections.Count == MaxSections)
            {
                break;
            }
        }

        return outline.Sections.Count == 0 ? null : outline;
    }

    public static IReadOnlyList<DraftQuestion> ParseQuestions(string text)
    {
        var root = ReadObject(text);

        if (root?["questions"] is not JsonArray items)
        {
            return [];
        }

        var questions = new List<DraftQuestion>();

        foreach (var node in items.OfType<JsonObject>())
        {
            var type = QuestionTypes.FirstOrDefault(candidate => string.Equals(candidate, String(node["type"], 50), StringComparison.OrdinalIgnoreCase));
            var prompt = String(node["prompt"], MaxTextLength);

            if (type is null || prompt is null)
            {
                continue;
            }

            var points = Number(node["points"]) ?? 1;
            var question = new DraftQuestion
            {
                Type = type,
                Prompt = prompt,
                Feedback = String(node["feedback"], MaxTextLength),
                Points = points is > 0 and <= 100 ? points : 1,
                Answer = Bool(node["answer"]),
            };

            if (type is "SingleChoice" or "MultipleChoice")
            {
                if (node["options"] is JsonArray options)
                {
                    foreach (var option in options.OfType<JsonObject>())
                    {
                        var optionText = String(option["text"], MaxTitleLength);

                        if (optionText is not null && question.Options.Count < MaxOptions)
                        {
                            question.Options.Add(new DraftOption { Text = optionText, Correct = Bool(option["correct"]) });
                        }
                    }
                }

                var correct = question.Options.Count(option => option.Correct);

                // A choice question needs two options or more and a right answer the type can hold.
                if (question.Options.Count < 2 || correct == 0 || (type == "SingleChoice" && correct != 1))
                {
                    continue;
                }
            }

            if (type == "ShortText")
            {
                if (node["acceptedAnswers"] is JsonArray answers)
                {
                    foreach (var answer in answers)
                    {
                        var answerText = String(answer, MaxTitleLength);

                        if (answerText is not null)
                        {
                            question.AcceptedAnswers.Add(answerText);
                        }
                    }
                }

                if (question.AcceptedAnswers.Count == 0)
                {
                    continue;
                }
            }

            questions.Add(question);

            if (questions.Count == MaxQuestions)
            {
                break;
            }
        }

        return questions;
    }

    public static DraftRubric ParseRubric(string text)
    {
        var root = ReadObject(text);
        var title = String(root?["title"], MaxTitleLength);

        if (title is null || root["criteria"] is not JsonArray criteria)
        {
            return null;
        }

        var rubric = new DraftRubric { Title = title };

        foreach (var node in criteria.OfType<JsonObject>())
        {
            var criterionTitle = String(node["title"], MaxTitleLength);

            if (criterionTitle is null || node["levels"] is not JsonArray levels)
            {
                continue;
            }

            var criterion = new DraftCriterion
            {
                Title = criterionTitle,
                Description = String(node["description"], MaxTextLength),
            };

            foreach (var level in levels.OfType<JsonObject>())
            {
                var levelTitle = String(level["title"], MaxTitleLength);
                var points = Number(level["points"]);

                if (levelTitle is not null && points is >= 0 and <= 1000 && criterion.Levels.Count < MaxLevels)
                {
                    criterion.Levels.Add(new DraftLevel { Title = levelTitle, Points = points.Value });
                }
            }

            if (criterion.Levels.Count >= 2)
            {
                rubric.Criteria.Add(criterion);
            }

            if (rubric.Criteria.Count == MaxCriteria)
            {
                break;
            }
        }

        return rubric.Criteria.Count == 0 ? null : rubric;
    }

    public static DraftTranslation ParseTranslation(string text)
    {
        var root = ReadObject(text);
        var title = String(root?["title"], MaxTitleLength);
        var body = String(root?["body"], MaxBodyLength);

        return title is null || body is null ? null : new DraftTranslation { Title = title, Body = body };
    }

    /// <summary>
    /// Reads the JSON object in a model's answer, which may be wrapped in a code fence or in words.
    /// </summary>
    /// <param name="text">The answer.</param>
    /// <returns>The object, or <see langword="null"/>.</returns>
    internal static JsonObject ReadObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var start = text.IndexOf('{', StringComparison.Ordinal);
        var end = text.LastIndexOf('}');

        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(text[start..(end + 1)]) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal static string String(JsonNode node, int maxLength)
    {
        if (node is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();

        return text.Length > maxLength ? text[..maxLength] : text;
    }

    internal static decimal? Number(JsonNode node)
    {
        if (node is not JsonValue value)
        {
            return null;
        }

        if (value.TryGetValue<decimal>(out var number))
        {
            return number;
        }

        return value.TryGetValue<string>(out var text) && decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out number) ? number : null;
    }

    internal static bool Bool(JsonNode node)
        => node is JsonValue value && ((value.TryGetValue<bool>(out var flag) && flag) || (value.TryGetValue<string>(out var text) && bool.TryParse(text, out flag) && flag));
}
