using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Works out when the time of the current page runs out, for pages with a time limit of their own.
/// </summary>
public static class SurveyPageTimer
{
    /// <summary>
    /// Gets when the time of the response's current page runs out: the time the respondent last entered it plus the
    /// page's time limit, capped by the response's own deadline.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <returns>The deadline, in UTC, or <see langword="null"/> when neither the page nor the response has one.</returns>
    public static DateTime? Deadline(SurveyDefinition definition, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        DateTime? deadline = response.DeadlineUtc is { } responseDeadline ? DateTime.SpecifyKind(responseDeadline, DateTimeKind.Utc) : null;
        var pageDeadline = PageDeadline(definition, response);

        return pageDeadline is not null && (deadline is null || pageDeadline < deadline) ? pageDeadline : deadline;
    }

    /// <summary>
    /// Gets whether the time of the current page ran out, allowing the survey's grace period for a post in flight.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the page's own time limit passed.</returns>
    public static bool PageTimeIsUp(SurveyDefinition definition, SurveyResponse response, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        var pageDeadline = PageDeadline(definition, response);
        var grace = Math.Max(definition?.Timing?.GraceSeconds ?? 5, 0);

        return pageDeadline is not null && utcNow >= pageDeadline.Value.AddSeconds(grace);
    }

    private static DateTime? PageDeadline(SurveyDefinition definition, SurveyResponse response)
    {
        var pageId = SurveyPlanNavigator.PageOf(response.Plan, response.CurrentPageId);
        var page = definition?.Pages?.FirstOrDefault(candidate => string.Equals(candidate.Id, pageId, StringComparison.Ordinal));

        if (page?.TimeLimitSeconds is not > 0)
        {
            return null;
        }

        var entered = response.PageVisits?.LastOrDefault(visit => string.Equals(visit.PageId, response.CurrentPageId, StringComparison.Ordinal))?.EnteredUtc;

        return entered is null
            ? null
            : DateTime.SpecifyKind(entered.Value, DateTimeKind.Utc).AddSeconds(page.TimeLimitSeconds.Value);
    }
}
