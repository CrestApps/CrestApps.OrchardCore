using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Builds the stable payloads of the Surveys webhook events.
/// </summary>
internal static class SurveyWebhookPayloads
{
    /// <summary>
    /// Builds the payload of a response event. An anonymous response carries no user, respondent or IP address.
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
            ["attemptNumber"] = response.AttemptNumber,
            ["score"] = response.Score,
            ["maxScore"] = response.MaxScore,
            ["percent"] = response.Percent,
            ["passed"] = response.Passed,
            ["startedUtc"] = response.StartedUtc,
            ["completedUtc"] = response.CompletedUtc,
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
