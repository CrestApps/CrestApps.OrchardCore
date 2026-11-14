namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Changes to a ticket's properties. A <see langword="null"/> value leaves the property as it is; an empty string
/// clears it where clearing is allowed.
/// </summary>
public sealed class TicketUpdate
{
    /// <summary>
    /// Gets or sets the new status.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the new priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the new desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the new team; empty clears it.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the new assignee; empty unassigns.
    /// </summary>
    public string AssigneeId { get; set; }

    /// <summary>
    /// Gets or sets the new subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the new escalation level.
    /// </summary>
    public int? EscalationLevel { get; set; }
}
