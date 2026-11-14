using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A status a ticket can be in, such as <em>Waiting for parts</em>, and what it means.
/// </summary>
public sealed class TicketStatus : CatalogItem, ITicketSettingsRecord
{
    /// <summary>
    /// Gets or sets the status's name, as agents see it.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the status means.
    /// </summary>
    public TicketStatusCategory Category { get; set; }

    /// <summary>
    /// Gets or sets what customers see instead of the name, such as <em>In progress</em>; empty shows the name.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets whether the ticket's service-level timers stop while it is in the status.
    /// </summary>
    public bool PausesTimers { get; set; }

    /// <summary>
    /// Gets or sets the color of its badge, a CSS color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets where it is listed; lower comes first.
    /// </summary>
    public int Order { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
