using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A help desk, such as customer support, IT or HR: its tickets, its team, its hours and its rules, kept apart from the
/// other desks' by permissions.
/// </summary>
public sealed class TicketDesk : CatalogItem, ITicketSettingsRecord
{
    /// <summary>
    /// Gets or sets the desk's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets what the desk is for.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets who the desk serves.
    /// </summary>
    public TicketDeskAudience Audience { get; set; }

    /// <summary>
    /// Gets or sets the team new tickets go to when their type names none.
    /// </summary>
    public string DefaultTeamId { get; set; }

    /// <summary>
    /// Gets or sets the business hours schedule the desk keeps, as an hours source's schedule id; empty is around the clock.
    /// </summary>
    public string HoursScheduleId { get; set; }

    /// <summary>
    /// Gets or sets the service-level policy of the desk's tickets when neither the account nor the type gives one.
    /// </summary>
    public string ServiceLevelPolicyId { get; set; }

    /// <summary>
    /// Gets or sets the ticket types the desk offers; empty offers every ticket type.
    /// </summary>
    public List<string> TicketTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets how long after solving a reply from the requester reopens the ticket, in days.
    /// </summary>
    public int ReopenWindowDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets how long a solved ticket waits before it closes, in days; 0 never closes it on its own.
    /// </summary>
    public int AutoCloseDays { get; set; } = 4;

    /// <summary>
    /// Gets or sets whether the requester is told by e-mail that their ticket was received, answered and solved.
    /// </summary>
    public bool NotifyRequester { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an agent is told when a ticket is assigned to them.
    /// </summary>
    public bool NotifyAssignee { get; set; } = true;

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
