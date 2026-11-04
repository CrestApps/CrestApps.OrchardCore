using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// The schedule of a team for some weeks, planned in draft scenarios and published to the team's calendar.
/// </summary>
public sealed class SchedulePeriod : CatalogItem
{
    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets a name, such as November weeks 1 and 2.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets the state.
    /// </summary>
    public SchedulePeriodStatus Status { get; set; } = SchedulePeriodStatus.Draft;

    /// <summary>
    /// Gets or sets the scenario that is published.
    /// </summary>
    public string PrimaryScenarioId { get; set; }

    /// <summary>
    /// Gets or sets how many times it has been published.
    /// </summary>
    public int PublishedVersion { get; set; }

    /// <summary>
    /// Gets or sets when it was last published.
    /// </summary>
    public DateTime? PublishedUtc { get; set; }

    /// <summary>
    /// Gets or sets who last published it.
    /// </summary>
    public string PublishedBy { get; set; }

    /// <summary>
    /// Gets or sets when it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}
