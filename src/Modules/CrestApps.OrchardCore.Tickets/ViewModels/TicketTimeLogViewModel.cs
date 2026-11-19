namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The Log time form of a ticket's Time panel.
/// </summary>
public class TicketTimeLogViewModel
{
    /// <summary>
    /// Gets or sets the time spent, as the person wrote it: <c>30</c>, <c>45m</c>, <c>1h 30m</c> or <c>1:30</c>.
    /// </summary>
    public string Spent { get; set; }

    /// <summary>
    /// Gets or sets when the work started, in the person's time zone, or <see langword="null"/> when it just ended.
    /// </summary>
    public DateTime? Started { get; set; }

    /// <summary>
    /// Gets or sets what was done.
    /// </summary>
    public string Note { get; set; }

    /// <summary>
    /// Gets or sets whether the time is billed to the customer.
    /// </summary>
    public bool Billable { get; set; } = true;
}
