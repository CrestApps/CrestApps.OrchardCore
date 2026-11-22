using System.Globalization;
using CrestApps.OrchardCore.Surveys.Controls;
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
    /// <returns>The input.</returns>
    public Dictionary<string, object> ForResponse(SurveyResponseEventContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var response = context.Response;
        var survey = context.Survey;
        var definition = context.Version?.Definition;
        var anonymous = survey?.Access?.Anonymity == AnonymityMode.Anonymous;
        var input = ForSurvey(survey);

        input["ResponseId"] = response.ItemId;
        input["Number"] = response.Number;
        input["VersionNumber"] = response.VersionNumber;
        input[SurveyWorkflowInput.CollectorId] = response.CollectorId;
        input[SurveyWorkflowInput.ContextKind] = response.Context?.Kind;
        input["ContextId"] = response.Context?.Id;
        input["Context"] = response.Context?.ToString();
        input["UserId"] = anonymous ? null : response.UserId;
        input["Respondent"] = anonymous ? null : response.Respondent?.ToString();
        input["Status"] = response.Status.ToString();
        input["Grading"] = response.Grading.ToString();
        input["AttemptNumber"] = response.AttemptNumber;
        input["Score"] = response.Score;
        input["MaxScore"] = response.MaxScore;
        input["Percent"] = response.Percent;
        input["Passed"] = response.Passed;
        input["IsTest"] = response.IsTest;
        input["CompletedUtc"] = response.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture);
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
