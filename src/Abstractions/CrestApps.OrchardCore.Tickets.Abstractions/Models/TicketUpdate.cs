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
    /// Gets or sets whether the update is refused when someone other than <see cref="AssigneeId"/> already has the
    /// ticket, checked inside the ticket's lock: work accepted from a queue doesn't take a ticket from whoever took it
    /// meanwhile.
    /// </summary>
    public bool OnlyIfUnassigned { get; set; }

    /// <summary>
    /// Gets or sets the new subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the new escalation level.
    /// </summary>
    public int? EscalationLevel { get; set; }

    /// <summary>
    /// Gets or sets whether to raise the escalation level by one, read and changed inside the ticket's lock.
    /// </summary>
    public bool RaiseEscalation { get; set; }

    /// <summary>
    /// Gets or sets why the change was made, written with it on the timeline, such as "Solved with its problem T-12".
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets the new impact; <see langword="null"/> leaves it, unless <see cref="SetPlanning"/>. With the
    /// urgency, it sets the priority from the desk's priority matrix when the update names no priority.
    /// </summary>
    public TicketLevel? Impact { get; set; }

    /// <summary>
    /// Gets or sets the new urgency; <see langword="null"/> leaves it, unless <see cref="SetPlanning"/>.
    /// </summary>
    public TicketLevel? Urgency { get; set; }

    /// <summary>
    /// Gets or sets the new billing type; <see langword="null"/> leaves it, unless <see cref="SetPlanning"/>.
    /// </summary>
    public TicketBillingType? BillingType { get; set; }

    /// <summary>
    /// Gets or sets whether <see cref="EstimatedStartUtc"/> and <see cref="EstimatedMinutes"/> replace the ticket's
    /// estimate, a <see langword="null"/> clearing it.
    /// </summary>
    public bool SetEstimate { get; set; }

    /// <summary>
    /// Gets or sets whether every planning value (impact, urgency, billing type and the estimate) replaces the ticket's,
    /// a <see langword="null"/> clearing it, as the planning panel sends them.
    /// </summary>
    public bool SetPlanning { get; set; }

    /// <summary>
    /// Gets or sets when work is planned to start, in UTC.
    /// </summary>
    public DateTime? EstimatedStartUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the work is expected to take, in minutes.
    /// </summary>
    public int? EstimatedMinutes { get; set; }
}
