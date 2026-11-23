using System.Globalization;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Learning.Assessments.Models;
using CrestApps.OrchardCore.Surveys.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Learning.Assessments.Core.Upgrades;

/// <summary>
/// Upgrades the quizzes of the first Learning release, whose questions were content items in question banks and whose
/// settings lived on the quiz, to quizzes that own a scored survey of the Surveys module.
/// </summary>
public static class LegacyQuizUpgrade
{
    /// <summary>
    /// The content types of the first release that are gone: the question bank and its 13 question types.
    /// </summary>
    public static readonly IReadOnlyList<string> RemovedContentTypes =
    [
        "QuestionBank",
        "SingleChoiceQuestion",
        "MultipleChoiceQuestion",
        "TrueFalseQuestion",
        "ShortTextQuestion",
        "NumericQuestion",
        "MatchingQuestion",
        "OrderingQuestion",
        "FillInBlanksQuestion",
        "EssayQuestion",
        "SurveyQuestion",
        "DragIntoTextQuestion",
        "GapSelectQuestion",
        "DescriptionQuestion",
    ];

    /// <summary>
    /// The content parts of the first release that are gone: the parts of the question types.
    /// </summary>
    public static readonly IReadOnlyList<string> RemovedContentParts =
    [
        "QuestionPart",
        "ChoiceQuestionPart",
        "TrueFalseQuestionPart",
        "ShortTextQuestionPart",
        "NumericQuestionPart",
        "MatchingQuestionPart",
        "OrderingQuestionPart",
        "FillInBlanksQuestionPart",
        "EssayQuestionPart",
        "SurveyQuestionPart",
        "ChoiceBlanksQuestionPart",
    ];

    // The quiz settings of the first release, which now live on the survey.
    private static readonly string[] _legacyKeys =
    [
        "QuestionIds", "DrawRules", "ShuffleQuestions", "TimeLimitMinutes", "MaxAttempts", "DelayMinutes", "PassMark",
        "Navigation", "OpensUtc", "ClosesUtc", "HonorStatement", "ReviewPolicy", "ReviewOptions", "QuestionsPerPage",
        "FeedbackMode", "AccessCode", "AccessCodeHash",
    ];

    /// <summary>
    /// Gets the quiz part of an item version as it was saved, or <see langword="null"/>.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <returns>The part.</returns>
    public static JsonObject PartOf(ContentItem item)
        => item is null ? null : ((JsonObject)item.Content)?[nameof(QuizPart)] as JsonObject;

    /// <summary>
    /// Gets whether a quiz item version still holds first-release settings that its survey hasn't taken yet.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <returns><see langword="true"/> when it holds any.</returns>
    public static bool HoldsLegacySettings(ContentItem item)
        => PartOf(item) is { } part && _legacyKeys.Any(part.ContainsKey);

    /// <summary>
    /// Points a quiz item version at its survey and leaves its first-release settings in place, so a survey that fails
    /// to take them can take them on the next run.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <param name="surveyId">The survey.</param>
    public static void PointAt(ContentItem item, string surveyId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var content = (JsonObject)item.Content;

        if (content[nameof(QuizPart)] is not JsonObject part)
        {
            part = [];
            content[nameof(QuizPart)] = part;
        }

        part[nameof(QuizPart.SurveyId)] = surveyId;
    }

    /// <summary>
    /// Gets whether a quiz item version still needs a survey.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <returns><see langword="true"/> when it has no survey.</returns>
    public static bool NeedsSurvey(ContentItem item)
        => PartOf(item) is not { } part
            || part[nameof(QuizPart.SurveyId)] is not JsonValue value
            || !value.TryGetValue<string>(out var surveyId)
            || string.IsNullOrEmpty(surveyId);

    /// <summary>
    /// Carries the settings of a first-release quiz to its new survey: the pass mark, the time limit, the attempt limit
    /// and the window. The questions are not carried: question banks are gone.
    /// </summary>
    /// <param name="legacy">The quiz part as it was saved.</param>
    /// <param name="survey">The quiz's new survey.</param>
    public static void ApplySettings(JsonObject legacy, Survey survey)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (legacy is null)
        {
            return;
        }

        var draft = survey.Draft ??= new SurveyDefinition();
        draft.Scoring ??= new SurveyScoringSettings();
        draft.Timing ??= new SurveyTimingSettings();
        draft.Scoring.Enabled = true;

        if (TryGet<decimal>(legacy["PassMark"], out var passMark) && passMark is >= 0 and <= 100)
        {
            draft.Scoring.PassMarkPercent = passMark;
        }

        if (TryGet<int>(legacy["TimeLimitMinutes"], out var minutes) && minutes > 0)
        {
            draft.Timing.TimeLimitMinutes = minutes;
        }

        survey.Access ??= new SurveyAccessSettings();
        survey.Access.Limits ??= new RespondentLimits();

        // The first release allowed one attempt unless the quiz said otherwise; null meant no limit.
        if (!legacy.ContainsKey("MaxAttempts"))
        {
            survey.Access.Limits.Total = 1;
        }
        else if (TryGet<int>(legacy["MaxAttempts"], out var attempts) && attempts > 0)
        {
            survey.Access.Limits.Total = attempts;
        }

        survey.Access.Limits.Counts = LimitCount.Started;

        if (TryGetUtc(legacy["OpensUtc"], out var opens))
        {
            survey.Access.OpensUtc = DateTime.SpecifyKind(opens, DateTimeKind.Utc);
        }

        if (TryGetUtc(legacy["ClosesUtc"], out var closes))
        {
            survey.Access.ClosesUtc = DateTime.SpecifyKind(closes, DateTimeKind.Utc);
        }
    }

    /// <summary>
    /// Points a quiz item version at its survey and drops the first release's settings from the part.
    /// </summary>
    /// <param name="item">The content item version.</param>
    /// <param name="surveyId">The survey.</param>
    public static void SetSurveyId(ContentItem item, string surveyId)
    {
        ArgumentNullException.ThrowIfNull(item);

        var content = (JsonObject)item.Content;

        if (content[nameof(QuizPart)] is not JsonObject part)
        {
            part = [];
            content[nameof(QuizPart)] = part;
        }

        foreach (var key in _legacyKeys)
        {
            part.Remove(key);
        }

        part[nameof(QuizPart.SurveyId)] = surveyId;
    }

    // A date read back from the database is a JSON element, but one built in memory holds its text.
    private static bool TryGetUtc(JsonNode node, out DateTime value)
    {
        if (node is JsonValue json)
        {
            if (json.TryGetValue(out value))
            {
                return true;
            }

            if (json.TryGetValue<string>(out var text)
                && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out value))
            {
                return true;
            }
        }

        value = default;

        return false;
    }

    private static bool TryGet<T>(JsonNode node, out T value)
    {
        if (node is JsonValue json && json.TryGetValue(out value))
        {
            return true;
        }

        value = default;

        return false;
    }
}
