using CrestApps.OrchardCore.ContactCenter.Core.Indexes;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Counts the dialer decisions of a period by kind and reason.
/// </summary>
/// <remarks>
/// A routing event can stand for many identical decisions, so a reason is counted by the decisions its events stand
/// for, not by the events. A coalesced event that began before the period, or is still being repeated after it, is
/// counted whole.
/// </remarks>
internal static class DialerDecisionReasonSummary
{
    /// <summary>
    /// Groups the decisions by kind and reason, the most frequent first.
    /// </summary>
    /// <param name="entries">The decisions of the period.</param>
    /// <returns>One row per kind and reason.</returns>
    public static IReadOnlyList<Row> Summarize(IEnumerable<DialerDecisionIndex> entries)
    {
        if (entries is null)
        {
            return [];
        }

        return entries
            .GroupBy(entry => (EventType: entry.EventType ?? string.Empty, Reason: entry.Reason ?? string.Empty))
            .Select(group => new Row(
                group.Key.EventType,
                group.Key.Reason,
                group.Sum(entry => Occurrences(entry)),
                group.Count(),
                group.Max(entry => entry.LastSeenUtc)))
            .OrderByDescending(row => row.Decisions)
            .ThenBy(row => row.EventType, StringComparer.Ordinal)
            .ThenBy(row => row.Reason, StringComparer.Ordinal)
            .ToArray();
    }

    /// <summary>
    /// Counts the decisions an entry stands for. An entry written before decisions were coalesced stands for one.
    /// </summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The number of decisions, at least one.</returns>
    public static int Occurrences(DialerDecisionIndex entry)
        => entry is not null && entry.OccurrenceCount > 0
            ? entry.OccurrenceCount
            : 1;

    /// <summary>
    /// Represents the decisions of one kind and reason.
    /// </summary>
    /// <param name="EventType">The kind of decision, as its event type.</param>
    /// <param name="Reason">The reason, as it is stored.</param>
    /// <param name="Decisions">How many decisions were made.</param>
    /// <param name="Events">How many events recorded them.</param>
    /// <param name="LastSeenUtc">When the last of them was made.</param>
    internal sealed record Row(string EventType, string Reason, int Decisions, int Events, DateTime LastSeenUtc);
}
