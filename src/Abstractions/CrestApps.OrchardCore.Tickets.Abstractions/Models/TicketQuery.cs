namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Which tickets to list, and in what order. Empty filters match every ticket the user may see.
/// </summary>
public sealed class TicketQuery
{
    /// <summary>
    /// Gets or sets the text to find in the number or subject.
    /// </summary>
    public string Search { get; set; }

    /// <summary>
    /// Gets or sets the desks to list.
    /// </summary>
    public List<string> DeskIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the status categories to list.
    /// </summary>
    public List<TicketStatusCategory> Categories { get; set; } = [];

    /// <summary>
    /// Gets or sets the statuses to list.
    /// </summary>
    public List<string> StatusIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the priorities to list.
    /// </summary>
    public List<string> PriorityIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the teams to list.
    /// </summary>
    public List<string> TeamIds { get; set; } = [];

    /// <summary>
    /// Gets or sets whose tickets to list: a user id, <see cref="TicketAssignees.Me"/> or
    /// <see cref="TicketAssignees.Unassigned"/>; empty lists everybody's.
    /// </summary>
    public string Assignee { get; set; }

    /// <summary>
    /// Gets or sets the ticket types to list.
    /// </summary>
    public List<string> ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the requester to list, as <c>Kind:Id</c>.
    /// </summary>
    public string Requester { get; set; }

    /// <summary>
    /// Gets or sets the account to list.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets a span: only tickets with a service-level timer due within it, or overdue, are listed.
    /// </summary>
    public TimeSpan? DueWithin { get; set; }

    /// <summary>
    /// Gets or sets the order.
    /// </summary>
    public TicketSort Sort { get; set; } = TicketSort.Updated;

    /// <summary>
    /// Gets or sets how many to skip.
    /// </summary>
    public int Skip { get; set; }

    /// <summary>
    /// Gets or sets how many to take.
    /// </summary>
    public int Take { get; set; } = 50;
}
