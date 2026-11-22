using CrestApps.OrchardCore.Surveys.Models;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Works out when the time of a screen runs out, for pages with a time limit of their own. The time is measured from the
/// respondent's first visit to any screen of the page, so leaving it and coming back never starts the time again, and a
/// page split into several screens gets its time limit once, not once per screen.
/// </summary>
public static class SurveyPageTimer
{
    /// <summary>
    /// Gets when the time of the response's current page runs out: the time the respondent first entered it plus the
    /// page's time limit, capped by the response's own deadline.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <returns>The deadline, in UTC, or <see langword="null"/> when neither the page nor the response has one.</returns>
    public static DateTime? Deadline(SurveyDefinition definition, SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        DateTime? deadline = response.DeadlineUtc is { } responseDeadline ? DateTime.SpecifyKind(responseDeadline, DateTimeKind.Utc) : null;
        var pageDeadline = PageDeadline(definition, response, response.CurrentPageId);

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

        return ScreenTimeIsUp(definition, response, response.CurrentPageId, utcNow);
    }

    /// <summary>
    /// Gets whether the time of a screen the respondent visited ran out, allowing the survey's grace period for a post in
    /// flight.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="response">The response.</param>
    /// <param name="screenId">The screen.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns><see langword="true"/> when the page of the screen has a time limit and it passed.</returns>
    public static bool ScreenTimeIsUp(SurveyDefinition definition, SurveyResponse response, string screenId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(response);

        var pageDeadline = PageDeadline(definition, response, screenId);
        var grace = Math.Max(definition?.Timing?.GraceSeconds ?? 5, 0);

        return pageDeadline is not null && utcNow >= pageDeadline.Value.AddSeconds(grace);
    }

    private static DateTime? PageDeadline(SurveyDefinition definition, SurveyResponse response, string screenId)
    {
        if (string.IsNullOrEmpty(screenId))
        {
            return null;
        }

        var pageId = SurveyPlanNavigator.PageOf(response.Plan, screenId);
        var page = definition?.Pages?.FirstOrDefault(candidate => string.Equals(candidate.Id, pageId, StringComparison.Ordinal));

        if (page?.TimeLimitSeconds is not > 0)
        {
            return null;
        }

        // The first visit to any screen of the page: a page split into several screens (questions per page) shares one
        // time limit, and going back and forth adds visits but never gives the page more time.
        var entered = response.PageVisits?
            .Where(visit => visit is not null && string.Equals(SurveyPlanNavigator.PageOf(response.Plan, visit.PageId), pageId, StringComparison.Ordinal))
            .Select(visit => (DateTime?)visit.EnteredUtc)
            .Min();

        return entered is null
            ? null
            : DateTime.SpecifyKind(entered.Value, DateTimeKind.Utc).AddSeconds(page.TimeLimitSeconds.Value);
    }
}
