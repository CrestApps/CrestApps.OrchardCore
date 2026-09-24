using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// The rules every report built on the event log applies to its period.
/// </summary>
internal static class ContactCenterReportPeriod
{
    /// <summary>
    /// Gets the end of the time a report can observe: the end of the period, or now when the period has not ended.
    /// </summary>
    /// <param name="toUtc">The end of the period, when one was chosen.</param>
    /// <param name="nowUtc">The current time.</param>
    /// <returns>The earlier of the two.</returns>
    public static DateTime ObservedEnd(DateTime? toUtc, DateTime nowUtc)
        => toUtc.HasValue && toUtc.Value < nowUtc ? toUtc.Value : nowUtc;

    /// <summary>
    /// Splits intervals at each UTC midnight, so a day's total holds only that day's time.
    /// </summary>
    /// <param name="intervals">The intervals.</param>
    /// <returns>The intervals, each within one UTC day.</returns>
    public static List<AgentPresenceInterval> SplitByUtcDay(IEnumerable<AgentPresenceInterval> intervals)
    {
        var result = new List<AgentPresenceInterval>();

        foreach (var interval in intervals)
        {
            var start = interval.StartUtc;

            while (start < interval.EndUtc)
            {
                var nextDay = start.Date.AddDays(1);
                var end = interval.EndUtc < nextDay ? interval.EndUtc : nextDay;

                result.Add(new AgentPresenceInterval
                {
                    AgentId = interval.AgentId,
                    Status = interval.Status,
                    Reason = interval.Reason,
                    QueueIds = [.. interval.QueueIds],
                    CampaignIds = [.. interval.CampaignIds],
                    StartUtc = start,
                    EndUtc = end,
                    FromAudit = interval.FromAudit,
                });

                start = end;
            }
        }

        return result;
    }

    /// <summary>
    /// Splits signed-in spans at each UTC midnight.
    /// </summary>
    /// <param name="spans">The spans.</param>
    /// <returns>The spans, each within one UTC day. Only the last piece of a span keeps its sign-off.</returns>
    public static List<AgentSignedInSpan> SplitByUtcDay(IEnumerable<AgentSignedInSpan> spans)
    {
        var result = new List<AgentSignedInSpan>();

        foreach (var span in spans)
        {
            var start = span.StartUtc;

            while (start < span.EndUtc)
            {
                var nextDay = start.Date.AddDays(1);
                var end = span.EndUtc < nextDay ? span.EndUtc : nextDay;

                result.Add(new AgentSignedInSpan(span.AgentId, start, end, span.EndedBySignOff && end == span.EndUtc));

                start = end;
            }
        }

        return result;
    }
}
