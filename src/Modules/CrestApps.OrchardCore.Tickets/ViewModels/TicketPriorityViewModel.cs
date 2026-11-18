namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The editor of a ticket priority.
/// </summary>
public class TicketPriorityViewModel
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the rank; higher is more urgent.
    /// </summary>
    public int Rank { get; set; }

    /// <summary>
    /// Gets or sets the badge color.
    /// </summary>
    public string Color { get; set; }
}
