using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// A rotation: a repeating set of weeks, each day either off or worked on a shift template, followed by some of a team's workers.
/// </summary>
public sealed class ShiftPattern : CatalogItem, INameAwareModel
{
    /// <summary>
    /// Gets or sets the rotation name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team whose schedules use it.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets how many weeks the rotation lasts before it repeats.
    /// </summary>
    public int WeekCount { get; set; } = 1;

    /// <summary>
    /// Gets or sets the worked days; days not listed are off.
    /// </summary>
    public IList<ShiftPatternDay> Days { get; set; } = [];

    /// <summary>
    /// Gets or sets the workers who follow it.
    /// </summary>
    public IList<ShiftPatternWorker> Workers { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the rotation is used.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
