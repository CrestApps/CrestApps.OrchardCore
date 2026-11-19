namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// One cell of a desk's priority matrix: the priority of a ticket with this impact and urgency.
/// </summary>
public sealed class TicketPriorityRule
{
    /// <summary>
    /// Gets or sets the impact.
    /// </summary>
    public TicketLevel Impact { get; set; }

    /// <summary>
    /// Gets or sets the urgency.
    /// </summary>
    public TicketLevel Urgency { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string PriorityId { get; set; }
}
