using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;

namespace CrestApps.OrchardCore.Surveys.Workflows;

/// <summary>
/// Builds the input of the Surveys workflow events. Anonymous responses carry no identity; the survey's secrets never
/// enter workflow state.
/// </summary>
internal sealed class SurveyWorkflowInputBuilder
{
    private readonly ISurveyControlRegistry _registry;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyWorkflowInputBuilder"/> class.
    /// </summary>
    /// <param name="registry">The question type registry.</param>
    public SurveyWorkflowInputBuilder(ISurveyControlRegistry registry)
    {
        _registry = registry;
    }

    /// <summary>
    /// Builds the input of a response event.
    /// </summary>
    /// <param name="context">The event.</param>
    /// <param name="shows">What an anonymous response may show of its collector, version and context kind
    /// (<see cref="SurveyAnonymousAttributes"/>), or <see langword="null"/> for all of them.</param>
    /// <param name="timeZone">The site's time zone, which an anonymous response's day is read in, or
    /// <see langword="null"/> for UTC.</param>
    /// <returns>The input.</returns>
    public Dictionary<string, object> ForResponse(SurveyResponseEventContext context, SurveyRowAttributes? shows = null, TimeZoneInfo timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.Response;
        var survey = context.Survey;
        var definition = context.Version?.Definition;
        var anonymous = survey?.Access?.Anonymity == AnonymityMode.Anonymous;
        var attributes = anonymous ? shows ?? SurveyRowAttributes.All : SurveyRowAttributes.All;
        var input = ForSurvey(survey);

        input["ResponseId"] = response.ItemId;
        // No site-wide response number on an anonymous survey: it would sit next to a named response's number.
        input["Number"] = anonymous ? null : response.Number;

        // An anonymous response's collector, version and context show only when enough completed responses share their
        // joint key: a link sent to one person, or a version one person answered through a link, would name them.
        input["VersionNumber"] = attributes.ShowsVersion ? response.VersionNumber : null;
        input[SurveyWorkflowInput.CollectorId] = attributes.ShowsCollector ? response.CollectorId : null;
        input[SurveyWorkflowInput.ContextKind] = attributes.ShowsContextKind ? response.Context?.Kind : null;
        // An anonymous response carries its context kind only, never the id, as the data sets and the API.
        input["ContextId"] = anonymous || !attributes.ShowsContextKind ? null : response.Context?.Id;
        input["Context"] = anonymous || !attributes.ShowsContextKind ? null : response.Context?.ToString();
        input["UserId"] = anonymous ? null : response.UserId;
        input["Respondent"] = anonymous ? null : response.Respondent?.ToString();
        input["Status"] = response.Status.ToString();
        input["Grading"] = response.Grading.ToString();
        // An anonymous response carries no attempt number (it ties a person's responses together) and its completion
        // time by the day only (in the site's time zone), as every other output of an anonymous survey.
        input["AttemptNumber"] = anonymous ? null : response.AttemptNumber;
        input["Score"] = response.Score;
        input["MaxScore"] = response.MaxScore;
        input["Percent"] = response.Percent;
        input["Passed"] = response.Passed;
        input["IsTest"] = response.IsTest;
        input["CompletedUtc"] = (anonymous ? DayOf(response.CompletedUtc, timeZone) : response.CompletedUtc)?.ToString("O", CultureInfo.InvariantCulture);
        input[SurveyWorkflowInput.Actor] = context.CausedByWorkflow ? SurveyWorkflowInput.WorkflowActor : context.ActorId ?? (anonymous ? null : response.UserId);
        input[SurveyWorkflowInput.CausedByWorkflow] = context.CausedByWorkflow;
        input[SurveyWorkflowInput.Anonymous] = anonymous;

        if (!string.IsNullOrEmpty(response.EndingId))
        {
            input[SurveyWorkflowInput.EndingName] = definition?.Endings.FirstOrDefault(ending => ending.Id == response.EndingId)?.Name;
        }

        if (!string.IsNullOrEmpty(context.PageId))
        {
            input[SurveyWorkflowInput.PageName] = definition?.Pages.FirstOrDefault(page => page.Id == context.PageId)?.Name
                ?? (response.Plan?.ScreenPages.TryGetValue(context.PageId, out var designed) == true ? definition?.Pages.FirstOrDefault(page => page.Id == designed)?.Name : null);
        }

        if (context.ScoreChange is { } change)
        {
            input["Change"] = change.Kind.ToString();
            input["From"] = change.From;
            input["To"] = change.To;
            input["Reason"] = change.Reason;
        }

        AddAnswers(input, definition, response);

        return input;
    }

    /// <summary>
    /// Gets the start of the day of a time in the site's time zone, as UTC, as every output of an anonymous response
    /// shows its times (<see cref="SurveyAnonymousDays"/>): one definition of the day, whichever output it is.
    /// </summary>
    /// <param name="utc">The time, in UTC.</param>
    /// <param name="timeZone">The site's time zone, or <see langword="null"/> for UTC.</param>
    /// <returns>The start of its day, or <see langword="null"/>.</returns>
    internal static DateTime? DayOf(DateTime? utc, TimeZoneInfo timeZone = null)
        => utc is { } value ? SurveyAnonymity.StartOfDayUtc(value, timeZone) : null;

    /// <summary>
    /// Builds the input of a survey event.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <returns>The input.</returns>
    public static Dictionary<string, object> ForSurvey(Survey survey)
        => new(StringComparer.Ordinal)
        {
            [SurveyWorkflowInput.SurveyId] = survey?.ItemId,
            [SurveyWorkflowInput.SurveyName] = survey?.Name,
            ["SurveyTitle"] = survey?.Title,
            [SurveyWorkflowInput.Kind] = survey?.Kind,
            ["SurveyStatus"] = survey?.Status.ToString(),
            ["PublishedVersionNumber"] = survey?.PublishedVersionNumber,
        };

    private void AddAnswers(Dictionary<string, object> input, SurveyDefinition definition, SurveyResponse response)
    {
        var answers = new Dictionary<string, string>(StringComparer.Ordinal);
        var answerValues = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var conditionValues = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (definition is not null)
        {
            var format = new SurveyFormatContext { Culture = CultureInfo.InvariantCulture, Definition = definition };

            foreach (var question in definition.Pages.SelectMany(page => page.Questions))
            {
                if (string.IsNullOrEmpty(question?.Id) || _registry.GetHandler(question.ControlType) is not { } handler)
                {
                    continue;
                }

                response.Answers.TryGetValue(question.Id, out var answer);

                var key = string.IsNullOrEmpty(question.Name) ? question.Id : question.Name;

                answers[key] = answer is null ? string.Empty : handler.FormatAnswer(question, answer, format) ?? string.Empty;
                answerValues[key] = answer?.Values?.ToArray() ?? [];

                var values = handler.GetConditionValue(question, answer, null)?.Values?.ToArray() ?? [];
                var withValues = WithChoiceValues(question, values);

                conditionValues[question.Id] = withValues;
                conditionValues[key] = withValues;

                foreach (var row in question.Rows)
                {
                    var rowValues = WithChoiceValues(question, handler.GetConditionValue(question, answer, row.Id)?.Values?.ToArray() ?? []);

                    conditionValues[question.Id + "." + row.Id] = rowValues;

                    if (!string.IsNullOrEmpty(row.Name))
                    {
                        conditionValues[key + "." + row.Name] = rowValues;
                    }
                }
            }
        }

        input["Answers"] = answers;
        input["AnswerValues"] = answerValues;
        input[SurveyWorkflowInput.ConditionValues] = conditionValues;
    }

    // A rule written by hand names choices by their export value as often as by id, so both are offered.
    private static string[] WithChoiceValues(SurveyQuestion question, string[] values)
    {
        if (question.Choices.Count == 0 || values.Length == 0)
        {
            return values;
        }

        var extra = values
            .Select(value => question.Choices.FirstOrDefault(choice => choice.Id == value)?.Value)
            .Where(value => !string.IsNullOrEmpty(value));

        return values.Concat(extra).Distinct(StringComparer.Ordinal).ToArray();
    }
}
