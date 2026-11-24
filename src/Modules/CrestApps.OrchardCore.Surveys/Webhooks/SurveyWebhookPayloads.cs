using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Webhooks;

/// <summary>
/// Builds the stable payloads of the Surveys webhook events.
/// </summary>
internal static class SurveyWebhookPayloads
{
    /// <summary>
    /// Builds the payload of a response event. An anonymous response carries no user, respondent, IP address or attempt
    /// number, its times by the day only (in the site's time zone), and its collector, version and context only when
    /// enough completed responses share their joint key.
    /// </summary>
    /// <param name="survey">The survey, or <see langword="null"/>.</param>
    /// <param name="response">The response.</param>
    /// <param name="shows">What an anonymous response may show of its collector, version and context kind
    /// (<see cref="Core.Access.SurveyAnonymousAttributes"/>), or <see langword="null"/> for all of them.</param>
    /// <param name="timeZone">The site's time zone, which an anonymous response's days are read in, or
    /// <see langword="null"/> for UTC.</param>
    /// <returns>The payload.</returns>
    public static Dictionary<string, object> Response(Survey survey, SurveyResponse response, SurveyRowAttributes? shows = null, TimeZoneInfo timeZone = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        var anonymous = survey?.Access?.Anonymity == AnonymityMode.Anonymous;
        var attributes = anonymous ? shows ?? SurveyRowAttributes.All : SurveyRowAttributes.All;

        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["responseId"] = response.ItemId,
            // No site-wide response number on an anonymous survey: it would sit next to a named response's number.
            ["number"] = anonymous ? null : response.Number,
            ["surveyId"] = response.SurveyId,
            ["surveyName"] = survey?.Name,
            ["kind"] = survey?.Kind,
            // A link sent to one person, or a version only one person answered through a link, would name them.
            ["versionNumber"] = attributes.ShowsVersion ? response.VersionNumber : null,
            ["status"] = response.Status.ToString(),
            ["grading"] = response.Grading.ToString(),
            ["userId"] = anonymous ? null : response.UserId,
            ["respondent"] = anonymous ? null : response.Respondent?.ToString(),
            ["collectorId"] = attributes.ShowsCollector ? response.CollectorId : null,
            ["context"] = attributes.ShowsContextKind ? response.Context?.ToString() : null,
            // An anonymous response carries no attempt number (it ties a person's responses together) and its times by
            // the day only, in the site's time zone. The delivery itself follows the response, so a receiver must not log
            // when it arrived.
            ["attemptNumber"] = anonymous ? null : response.AttemptNumber,
            ["score"] = response.Score,
            ["maxScore"] = response.MaxScore,
            ["percent"] = response.Percent,
            ["passed"] = response.Passed,
            ["startedUtc"] = anonymous ? Workflows.SurveyWorkflowInputBuilder.DayOf(response.StartedUtc, timeZone) : response.StartedUtc,
            ["completedUtc"] = anonymous ? Workflows.SurveyWorkflowInputBuilder.DayOf(response.CompletedUtc, timeZone) : response.CompletedUtc,
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
