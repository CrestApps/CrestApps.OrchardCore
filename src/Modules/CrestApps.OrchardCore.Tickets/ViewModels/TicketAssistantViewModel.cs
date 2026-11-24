namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The ticket page's AI assistant panel.
/// </summary>
public class TicketAssistantViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets whether the user may have a reply suggested.
    /// </summary>
    public bool CanSuggest { get; set; }

    /// <summary>
    /// Gets or sets whether the user may triage the ticket.
    /// </summary>
    public bool CanTriage { get; set; }
}
