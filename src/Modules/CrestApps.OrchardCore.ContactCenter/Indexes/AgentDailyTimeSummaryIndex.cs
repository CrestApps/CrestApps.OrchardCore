using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.ContactCenter.Reports.Models;

namespace CrestApps.OrchardCore.ContactCenter.Indexes;

/// <summary>
/// Represents the YesSql index the daily agent time summaries are found by: their day, the version of the computation
/// that produced them, and how many state changes they were computed from.
/// </summary>
public sealed class AgentDailyTimeSummaryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the document identifier.
    /// </summary>
    public long DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the UTC midnight that starts the day. See <see cref="AgentDailyTimeSummary.Date"/>.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the version of the computation. See <see cref="AgentDailyTimeSummary.Version"/>.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets how many state changes the summary was computed from. See <see cref="AgentDailyTimeSummary.EventCount"/>.
    /// </summary>
    public int EventCount { get; set; }
}
