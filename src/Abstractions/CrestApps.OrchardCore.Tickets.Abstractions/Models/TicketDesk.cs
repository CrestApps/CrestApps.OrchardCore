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
    /// Gets or sets the team availability sources assignment honors on this desk (their names), or <see langword="null"/>
    /// for the sources honored by default (shifts and absences, not Contact Center presence).
    /// </summary>
    public List<string> AvailabilitySources { get; set; }

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
    /// Gets or sets how a new ticket with a team and no assignee is given to a team member: a strategy of
    /// <c>TeamAssignmentStrategies</c> (least loaded, round robin), or empty to leave it for an agent to take.
    /// </summary>
    public string AssignmentStrategy { get; set; } = "LeastLoaded";

    /// <summary>
    /// Gets or sets whether the requester is told by e-mail that their ticket was received, answered and solved.
    /// </summary>
    public bool NotifyRequester { get; set; } = true;

    /// <summary>
    /// Gets or sets whether an agent is told when a ticket is assigned to them.
    /// </summary>
    public bool NotifyAssignee { get; set; } = true;

    /// <summary>
    /// Gets or sets how many hours after a ticket is solved its requester is asked to rate it (with Tickets Portal on and
    /// requesters e-mailed); 0 never asks.
    /// </summary>
    public int RatingRequestDelayHours { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the priority of a ticket for each impact and urgency; a pair it doesn't name leaves the priority as
    /// it is.
    /// </summary>
    public List<TicketPriorityRule> PriorityMatrix { get; set; } = [];

    /// <summary>
    /// Finds the priority the matrix gives an impact and an urgency.
    /// </summary>
    /// <param name="impact">The impact.</param>
    /// <param name="urgency">The urgency.</param>
    /// <returns>The priority, or <see langword="null"/> when either is unknown or the matrix doesn't name the pair.</returns>
    public string PriorityFor(TicketLevel? impact, TicketLevel? urgency)
        => impact is { } i && urgency is { } u
            ? PriorityMatrix?.FirstOrDefault(rule => rule.Impact == i && rule.Urgency == u && !string.IsNullOrEmpty(rule.PriorityId))?.PriorityId
            : null;

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
