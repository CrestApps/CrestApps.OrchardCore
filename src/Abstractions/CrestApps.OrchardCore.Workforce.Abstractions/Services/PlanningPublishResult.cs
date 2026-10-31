using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// What publishing a schedule period did.
/// </summary>
public sealed class PlanningPublishResult
{
    /// <summary>
    /// Gets or sets why shifts could not be published; empty when every shift was.
    /// </summary>
    public IList<string> Errors { get; set; } = [];

    /// <summary>
    /// Gets or sets how many shifts were added to the calendar.
    /// </summary>
    public int Added { get; set; }

    /// <summary>
    /// Gets or sets how many were changed.
    /// </summary>
    public int Updated { get; set; }

    /// <summary>
    /// Gets or sets how many were removed.
    /// </summary>
    public int Removed { get; set; }

    /// <summary>
    /// Gets or sets how many draft shifts have nobody, were left off the calendar and are offered as open shifts.
    /// </summary>
    public int Open { get; set; }

    /// <summary>
    /// Gets or sets the changes recorded against an already published schedule.
    /// </summary>
    public IList<ScheduleChange> Changes { get; set; } = [];

    /// <summary>
    /// Gets whether every shift was published.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
