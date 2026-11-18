using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// The ticket page's Planning panel.
/// </summary>
public class TicketPlanningViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the ticket's part.
    /// </summary>
    public TicketPart Part { get; set; }

    /// <summary>
    /// Gets or sets when work is planned to start, in the user's time zone.
    /// </summary>
    public DateTime? EstimatedStart { get; set; }

    /// <summary>
    /// Gets or sets the name of the person who solved the ticket.
    /// </summary>
    public string SolvedByName { get; set; }

    /// <summary>
    /// Gets or sets whether the desk's priority matrix sets the priority from the impact and urgency.
    /// </summary>
    public bool HasPriorityMatrix { get; set; }

    /// <summary>
    /// Gets or sets whether the user may change the values.
    /// </summary>
    public bool CanEdit { get; set; }

    /// <summary>
    /// Gets or sets the agreements of the ticket's account it may work under, when agreements are on.
    /// </summary>
    public IReadOnlyList<TicketAgreementInfo> Agreements { get; set; } = [];

    /// <summary>
    /// Gets or sets whether agreements are on.
    /// </summary>
    public bool HasAgreements { get; set; }
}
