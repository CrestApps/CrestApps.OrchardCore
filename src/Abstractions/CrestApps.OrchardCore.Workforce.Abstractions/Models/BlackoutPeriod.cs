using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Dates no time off can be requested for, such as a busy season, for a team (and its sub-teams) or everyone.
/// </summary>
public sealed class BlackoutPeriod : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the team it applies to, with its sub-teams; empty for every team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the first day.
    /// </summary>
    public DateOnly StartDate { get; set; }

    /// <summary>
    /// Gets or sets the last day.
    /// </summary>
    public DateOnly EndDate { get; set; }

    /// <summary>
    /// Gets or sets the time-off types it blocks; empty for every type.
    /// </summary>
    public IList<string> TypeIds { get; set; } = [];

    /// <summary>
    /// Gets or sets what a worker is told when a request falls in it.
    /// </summary>
    public string Message { get; set; }

    /// <summary>
    /// Gets or sets whether it is in use.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time it was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time it was last changed.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
