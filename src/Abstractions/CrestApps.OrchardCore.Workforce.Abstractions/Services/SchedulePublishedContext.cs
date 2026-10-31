using CrestApps.OrchardCore.Workforce.Models;

namespace CrestApps.OrchardCore.Workforce.Services;

/// <summary>
/// Says that a schedule period was published.
/// </summary>
public sealed class SchedulePublishedContext
{
    /// <summary>
    /// Gets or sets the period.
    /// </summary>
    public SchedulePeriod Period { get; set; }

    /// <summary>
    /// Gets or sets what the publish did.
    /// </summary>
    public PlanningPublishResult Result { get; set; }

    /// <summary>
    /// Gets or sets who published.
    /// </summary>
    public string Actor { get; set; }
}
