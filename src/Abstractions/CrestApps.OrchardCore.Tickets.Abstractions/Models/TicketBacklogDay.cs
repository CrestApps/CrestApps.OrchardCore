namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// The open tickets of one desk at the start of one day, kept so the backlog can be read back for any past day: history
/// is never worked out again from the tickets as they are now. Taken once a day by the backlog background task.
/// </summary>
public sealed class TicketBacklogDay
{
    /// <summary>
    /// Gets or sets the YesSql document id.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// Gets or sets the day, in the site's time zone, as <c>yyyy-MM-dd</c>.
    /// </summary>
    public string Day { get; set; }

    /// <summary>
    /// Gets or sets the desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets when the snapshot was taken.
    /// </summary>
    public DateTime TakenUtc { get; set; }

    /// <summary>
    /// Gets or sets the open tickets, counted by status, priority, team, assignee, ticket type and channel.
    /// </summary>
    public List<TicketBacklogGroup> Groups { get; set; } = [];
}

/// <summary>
/// The open tickets of a desk that share a status, priority, team, assignee, ticket type, channel and category.
/// </summary>
public sealed class TicketBacklogGroup
{
    public string StatusId { get; set; }

    public TicketStatusCategory StatusCategory { get; set; }

    public string PriorityId { get; set; }

    public string TeamId { get; set; }

    public string AssigneeId { get; set; }

    /// <summary>
    /// Gets or sets the category, for the days recorded since the backlog was broken down by category.
    /// </summary>
    public string CategoryId { get; set; }

    public string ContentType { get; set; }

    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets how many tickets.
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Gets or sets how many of them were past their next service-level target.
    /// </summary>
    public int Overdue { get; set; }

    /// <summary>
    /// Gets or sets the ages of the tickets added up, in hours.
    /// </summary>
    public double TotalAgeHours { get; set; }

    /// <summary>
    /// Gets or sets when the oldest of them was created.
    /// </summary>
    public DateTime OldestCreatedUtc { get; set; }
}
