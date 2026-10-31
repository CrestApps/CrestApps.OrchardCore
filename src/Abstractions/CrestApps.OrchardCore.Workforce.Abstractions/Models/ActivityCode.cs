using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a worker does during a segment of a shift: work, a break, a meal, training and so on. Whether it is paid,
/// counts as work and counts toward coverage drive the rules, timesheets and staffing.
/// </summary>
public sealed class ActivityCode : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the activity name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets a short code shown on schedules, such as BRK.
    /// </summary>
    public string Code { get; set; }

    /// <summary>
    /// Gets or sets what kind of activity it is.
    /// </summary>
    public ActivityKind Kind { get; set; } = ActivityKind.Work;

    /// <summary>
    /// Gets or sets the color it is drawn in.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets whether the time is paid.
    /// </summary>
    public bool IsPaid { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the time counts toward hours rules such as the weekly maximum.
    /// </summary>
    public bool CountsAsWork { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the worker counts toward staffing during it.
    /// </summary>
    public bool CountsTowardCoverage { get; set; } = true;

    /// <summary>
    /// Gets or sets whether the code can be scheduled.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
