using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// One agent's day on a timecard that proves itself: the time signed in, measured from sign-ins and sign-offs alone,
/// beside the time in each state, and whether the two agree.
/// </summary>
internal sealed class ReconciledTimecardDay
{
    /// <summary>
    /// The largest difference, in seconds, still counted as agreement: the floating-point noise of adding up
    /// intervals, far below the second a timecard is shown to.
    /// </summary>
    public const double Tolerance = 0.001;

    /// <summary>
    /// Gets the UTC day.
    /// </summary>
    public DateOnly Date { get; init; }

    /// <summary>
    /// Gets the agent profile identifier.
    /// </summary>
    public string AgentId { get; init; }

    /// <summary>
    /// Gets when the agent's first signed-in span of the day starts.
    /// </summary>
    public DateTime? FirstInUtc { get; init; }

    /// <summary>
    /// Gets when the agent's last signed-in span of the day ends.
    /// </summary>
    public DateTime? LastOutUtc { get; init; }

    /// <summary>
    /// Gets whether the day's last signed-in span ends with a sign-off, rather than at midnight or the end of the period.
    /// </summary>
    public bool EndsSignedOff { get; init; }

    /// <summary>
    /// Gets the time signed in, from sign-ins and sign-offs.
    /// </summary>
    public double SignedInSeconds { get; init; }

    /// <summary>
    /// Gets the time in each state.
    /// </summary>
    public AgentTimeSummary States { get; init; }

    /// <summary>
    /// Gets how many recorded changes did not leave the state the one before them entered.
    /// </summary>
    public int MissingTransitions { get; init; }

    /// <summary>
    /// Gets whether any of the day's time was read from the state audit.
    /// </summary>
    public bool FromAudit { get; init; }

    /// <summary>
    /// Gets whether any of the day's time was read from the older presence events.
    /// </summary>
    public bool FromLegacy { get; init; }

    /// <summary>
    /// Gets the time in every signed-in state.
    /// </summary>
    public double StateSeconds => States.SignedInSeconds;

    /// <summary>
    /// Gets the state time less the signed-in time: positive when states were recorded while the agent was not
    /// signed in, negative when signed-in time is not accounted for by any state.
    /// </summary>
    public double DifferenceSeconds => StateSeconds - SignedInSeconds;

    /// <summary>
    /// Gets whether the day reconciles: its states add up to its signed-in time and no transition is missing.
    /// </summary>
    public bool IsReconciled => Math.Abs(DifferenceSeconds) < Tolerance && MissingTransitions == 0;
}

/// <summary>
/// Builds the reconciled timecard from agent state timelines.
/// </summary>
internal static class ReconciledTimecard
{
    /// <summary>
    /// Builds one row per agent per UTC day with any signed-in time or state time in the period.
    /// </summary>
    /// <param name="timelines">The agents' state timelines.</param>
    /// <param name="fromUtc">The start of the period.</param>
    /// <param name="toUtc">The end of the period: an agent still signed in then is counted up to it.</param>
    /// <returns>The days, in date then agent order.</returns>
    public static IReadOnlyList<ReconciledTimecardDay> Build(IEnumerable<AgentStateTimeline> timelines, DateTime fromUtc, DateTime toUtc)
    {
        ArgumentNullException.ThrowIfNull(timelines);

        var days = new List<ReconciledTimecardDay>();

        foreach (var timeline in timelines)
        {
            var intervals = ContactCenterReportPeriod
                .SplitByUtcDay(AgentStateTimeline.BuildIntervals([timeline], fromUtc, toUtc))
                .Where(interval => interval.Status != AgentPresenceStatus.Offline)
                .ToLookup(interval => DateOnly.FromDateTime(interval.StartUtc));
            var spans = ContactCenterReportPeriod
                .SplitByUtcDay(timeline.BuildSignedInSpans(fromUtc, toUtc))
                .ToLookup(span => DateOnly.FromDateTime(span.StartUtc));
            var missing = timeline.Transitions
                .Where(transition => !transition.Superseded && transition.BreaksChain && transition.ChangedUtc >= fromUtc && transition.ChangedUtc <= toUtc)
                .ToLookup(transition => DateOnly.FromDateTime(transition.ChangedUtc));

            foreach (var date in intervals.Select(group => group.Key)
                .Concat(spans.Select(group => group.Key))
                .Concat(missing.Select(group => group.Key))
                .Distinct()
                .Order())
            {
                var dayIntervals = intervals[date].ToArray();
                var daySpans = spans[date].OrderBy(span => span.StartUtc).ToArray();

                days.Add(new ReconciledTimecardDay
                {
                    Date = date,
                    AgentId = timeline.AgentId,
                    FirstInUtc = daySpans.Length > 0 ? daySpans[0].StartUtc : null,
                    LastOutUtc = daySpans.Length > 0 ? daySpans[^1].EndUtc : null,
                    EndsSignedOff = daySpans.Length > 0 && daySpans[^1].EndedBySignOff,
                    SignedInSeconds = daySpans.Sum(span => span.DurationSeconds),
                    States = AgentTimeSummary.Create(dayIntervals),
                    MissingTransitions = missing[date].Count(),
                    FromAudit = dayIntervals.Any(interval => interval.FromAudit),
                    FromLegacy = dayIntervals.Any(interval => !interval.FromAudit),
                });
            }
        }

        return days
            .OrderBy(day => day.Date)
            .ThenBy(day => day.AgentId, StringComparer.Ordinal)
            .ToArray();
    }
}
