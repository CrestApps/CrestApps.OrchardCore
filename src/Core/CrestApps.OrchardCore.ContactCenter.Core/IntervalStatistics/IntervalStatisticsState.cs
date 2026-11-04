using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// The interval statistics' progress: the rebuild an administrator asked for and where it is, and the last run.
/// </summary>
public sealed class IntervalStatisticsState : CatalogItem
{
    /// <summary>
    /// Gets or sets the first day of the rebuild, or <see langword="null"/> when none is asked for.
    /// </summary>
    public DateTime? RebuildFromUtc { get; set; }

    /// <summary>
    /// Gets or sets the last day of the rebuild.
    /// </summary>
    public DateTime? RebuildToUtc { get; set; }

    /// <summary>
    /// Gets or sets the next day the rebuild computes.
    /// </summary>
    public DateTime? RebuildNextUtc { get; set; }

    /// <summary>
    /// Gets or sets who asked for the rebuild.
    /// </summary>
    public string RebuildRequestedBy { get; set; }

    /// <summary>
    /// Gets or sets when the rebuild was asked for.
    /// </summary>
    public DateTime? RebuildRequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets when a day was last computed.
    /// </summary>
    public DateTime? LastProgressUtc { get; set; }

    /// <summary>
    /// Gets or sets when the roller last ran.
    /// </summary>
    public DateTime? LastRunUtc { get; set; }

    /// <summary>
    /// Gets or sets the last run's error, or <see langword="null"/>.
    /// </summary>
    public string LastError { get; set; }

    /// <summary>
    /// Gets whether a rebuild is under way.
    /// </summary>
    public bool IsRebuilding => RebuildNextUtc.HasValue && RebuildToUtc.HasValue && RebuildNextUtc <= RebuildToUtc;
}
