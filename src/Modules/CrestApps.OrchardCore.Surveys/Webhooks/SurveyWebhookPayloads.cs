using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Builds the stable payloads of the Surveys webhook events.
/// </summary>
internal static class SurveyWebhookPayloads
{
    /// <summary>
    /// Builds the payload of a response event. An anonymous response carries no user, respondent, IP address or attempt
    /// number, and its times by the day only.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <param name="response">The response.</param>
    /// <returns>The payload.</returns>
    public static Dictionary<string, object> Response(Survey survey, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var anonymous = survey?.Access?.Anonymity == AnonymityMode.Anonymous;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["responseId"] = response.ItemId,
            // No site-wide response number on an anonymous survey: it would sit next to a named response's number.
            ["number"] = anonymous ? null : response.Number,
            ["surveyId"] = response.SurveyId,
            ["surveyName"] = survey?.Name,
            ["kind"] = survey?.Kind,
            ["versionNumber"] = response.VersionNumber,
            ["status"] = response.Status.ToString(),
            ["grading"] = response.Grading.ToString(),
            ["userId"] = anonymous ? null : response.UserId,
            ["respondent"] = anonymous ? null : response.Respondent?.ToString(),
            ["collectorId"] = response.CollectorId,
            ["context"] = response.Context?.ToString(),
            // An anonymous response carries no attempt number (it ties a person's responses together) and its times by
            // the day only, in UTC. The delivery itself follows the response, so a receiver must not log when it arrived.
            ["attemptNumber"] = anonymous ? null : response.AttemptNumber,
            ["score"] = response.Score,
            ["maxScore"] = response.MaxScore,
            ["percent"] = response.Percent,
            ["passed"] = response.Passed,
            ["startedUtc"] = anonymous ? Workflows.SurveyWorkflowInputBuilder.DayOf(response.StartedUtc) : response.StartedUtc,
            ["completedUtc"] = anonymous ? Workflows.SurveyWorkflowInputBuilder.DayOf(response.CompletedUtc) : response.CompletedUtc,
            ["properties"] = response.Properties,
        };
    }

    /// <summary>
    /// Builds the payload of the published event.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="version">The version, or <see langword="null"/>.</param>
    /// <returns>The payload.</returns>
    public static Dictionary<string, object> Published(Survey survey, SurveyVersion version)
    {
        ArgumentNullException.ThrowIfNull(survey);

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["surveyId"] = survey.ItemId,
            ["surveyName"] = survey.Name,
            ["title"] = survey.Title,
            ["kind"] = survey.Kind,
            ["versionNumber"] = version?.Number,
            ["publishedUtc"] = version?.PublishedUtc,
        };
    }
}
