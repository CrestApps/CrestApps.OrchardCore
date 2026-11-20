using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a ticket status.
/// </summary>
public class TicketStatusViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the status means.
    /// </summary>
    public TicketStatusCategory Category { get; set; }

    /// <summary>
    /// Gets or sets what customers see.
    /// </summary>
    public string CustomerLabel { get; set; }

    /// <summary>
    /// Gets or sets whether service-level timers stop in the status.
    /// </summary>
    public bool PausesTimers { get; set; }

    /// <summary>
    /// Gets or sets the badge color.
    /// </summary>
    public string Color { get; set; }

    /// <summary>
    /// Gets or sets where it is listed.
    /// </summary>
    public int Order { get; set; }
}
