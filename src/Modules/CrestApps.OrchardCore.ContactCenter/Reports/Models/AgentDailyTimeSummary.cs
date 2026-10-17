using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Models;

/// <summary>
/// One UTC day of every agent's time, by state: the summary the time-based workforce reports read for a whole day
/// inside their period instead of reading the day's state changes from the log.
/// </summary>
/// <remarks>
/// A summary is derived data. It is computed from the log by the same code a report runs over the log, it records how
/// many state changes it was computed from so a report can tell when the log has changed since, and it can be deleted
/// at any time: the day is then read from the log until it is computed again.
/// </remarks>
public sealed class AgentDailyTimeSummary : CatalogItem
{
    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the version of the computation that produced the summary. A summary of another version is not read.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets how many agent state changes the log held for the day when the summary was computed.
    /// </summary>
    public int EventCount { get; set; }

    /// <summary>
    /// Gets or sets when the summary was computed.
    /// </summary>
    public DateTime ComputedUtc { get; set; }

    /// <summary>
    /// Gets or sets each agent's time on the day.
    /// </summary>
    public List<AgentDailyTime> Agents { get; set; } = [];
}
