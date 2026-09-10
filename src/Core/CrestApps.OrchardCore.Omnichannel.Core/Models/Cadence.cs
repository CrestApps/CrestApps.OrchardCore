using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Represents a reusable re-engagement (nudge) schedule: a named, ordered list of follow-up messages sent to a contact
/// who has gone quiet in an automated conversation. It is selected on a loading campaign; a campaign with no schedule
/// selected never nudges. Every nudge still respects the campaign's business-hours calendar.
/// </summary>
public sealed class Cadence : CatalogItem, IDisplayTextAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the display text (name) of the schedule.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the description of the schedule.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the ordered steps. Each step sends one nudge after its configured silence; the number of steps is
    /// the cap on how many nudges are ever sent, so nudging is always finite. An empty list never nudges.
    /// </summary>
    public IList<CadenceStep> Steps { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether the schedule is enabled. A disabled schedule never nudges.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the UTC time the schedule was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the schedule was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
