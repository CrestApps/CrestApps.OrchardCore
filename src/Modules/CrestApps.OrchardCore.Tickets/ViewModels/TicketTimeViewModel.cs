using CrestApps.OrchardCore.Tickets.TimeTracker;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The ticket page's Time panel.
/// </summary>
public class TicketTimeViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the ticket's time.
    /// </summary>
    public TicketTimeSummary Summary { get; set; }

    /// <summary>
    /// Gets or sets whether the user may start or stop working on the ticket and log time on it.
    /// </summary>
    public bool CanWork { get; set; }

    /// <summary>
    /// Gets or sets whether the user may correct anyone's time entries in Time Tracker.
    /// </summary>
    public bool CanCorrect { get; set; }
}
