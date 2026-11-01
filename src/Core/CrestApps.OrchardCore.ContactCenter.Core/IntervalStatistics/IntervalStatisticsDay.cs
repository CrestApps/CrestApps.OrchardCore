using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// Records that one UTC day's interval statistics were computed, even when the day had no work, and whether the day
/// is final: computed once it had been over long enough for its work to end.
/// </summary>
public sealed class IntervalStatisticsDay : CatalogItem
{
    /// <summary>
    /// Gets or sets the UTC midnight that starts the day.
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the version of the computation.
    /// </summary>
    public int Version { get; set; }

    /// <summary>
    /// Gets or sets whether the day was over, and settled, when it was computed.
    /// </summary>
    public bool Final { get; set; }

    /// <summary>
    /// Gets or sets how many interactions the day had when it was computed, so a day that gains or loses some is
    /// computed again.
    /// </summary>
    public int InteractionCount { get; set; }

    /// <summary>
    /// Gets or sets when the day was computed.
    /// </summary>
    public DateTime ComputedUtc { get; set; }
}
