using CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// The interval statistics status page.
/// </summary>
public class IntervalStatisticsViewModel
{
    /// <summary>
    /// Gets or sets the roller's state.
    /// </summary>
    [BindNever]
    public IntervalStatisticsState State { get; set; }

    /// <summary>
    /// Gets or sets how many of the last 30 days are computed.
    /// </summary>
    [BindNever]
    public int DaysComputed { get; set; }

    /// <summary>
    /// Gets or sets how many of the last 30 days are final.
    /// </summary>
    [BindNever]
    public int DaysFinal { get; set; }

    /// <summary>
    /// Gets or sets today's computed day, or <see langword="null"/>.
    /// </summary>
    [BindNever]
    public IntervalStatisticsDay Today { get; set; }

    /// <summary>
    /// Gets or sets today's totals over every queue.
    /// </summary>
    [BindNever]
    public QueueIntervalStatistic TodayTotal { get; set; }

    /// <summary>
    /// Gets or sets how many queues had work today.
    /// </summary>
    [BindNever]
    public int QueuesToday { get; set; }

    /// <summary>
    /// Gets or sets how many days are kept.
    /// </summary>
    [BindNever]
    public int RetentionDays { get; set; }

    /// <summary>
    /// Gets or sets how many days are backfilled.
    /// </summary>
    [BindNever]
    public int BackfillDays { get; set; }

    /// <summary>
    /// Gets or sets whether the viewer may rebuild.
    /// </summary>
    [BindNever]
    public bool CanRebuild { get; set; }

    /// <summary>
    /// Gets or sets whether a rebuild has made no progress for a while.
    /// </summary>
    [BindNever]
    public bool Stalled { get; set; }

    /// <summary>
    /// Gets or sets the first day to rebuild.
    /// </summary>
    public DateTime RebuildFrom { get; set; }

    /// <summary>
    /// Gets or sets the last day to rebuild.
    /// </summary>
    public DateTime RebuildTo { get; set; }
}
