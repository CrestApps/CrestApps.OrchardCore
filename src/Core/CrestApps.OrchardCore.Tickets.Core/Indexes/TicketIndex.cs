using CrestApps.OrchardCore.Tickets.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tickets.Core.Indexes;

/// <summary>
/// The columns ticket lists, saved views and reports filter and sort on.
/// </summary>
public sealed class TicketIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the ticket's content item id.
    /// </summary>
    public string ContentItemId { get; set; }

    /// <summary>
    /// Gets or sets the ticket type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the ticket number.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the subject, cut to the column's length.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets the desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the status's category.
    /// </summary>
    public TicketStatusCategory StatusCategory { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the priority's rank, for sorting.
    /// </summary>
    public int PriorityRank { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the assignee.
    /// </summary>
    public string AssigneeId { get; set; }

    /// <summary>
    /// Gets or sets the requester, as <c>Kind:Id</c>.
    /// </summary>
    public string Requester { get; set; }

    /// <summary>
    /// Gets or sets the e-mail of a requester kept as a contact snapshot, in lower case, so a person's tickets are found
    /// by their e-mail.
    /// </summary>
    public string RequesterEmail { get; set; }

    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the channel it came in on.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the ticket it was merged into.
    /// </summary>
    public string MergedIntoId { get; set; }

    /// <summary>
    /// Gets or sets when it was opened, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it last changed, in UTC.
    /// </summary>
    public DateTime ModifiedUtc { get; set; }

    /// <summary>
    /// Gets or sets the earliest due time of its running service-level timers, in UTC.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }

    /// <summary>
    /// Gets or sets the next due time, or <see cref="TicketIndexProvider.NoDueUtc"/> when nothing is due, so the list
    /// can sort by due time with tickets that have none last, on every database (SQL Server can't order by a test for null).
    /// </summary>
    public DateTime? DueSortUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was last solved, in UTC.
    /// </summary>
    public DateTime? SolvedUtc { get; set; }

    /// <summary>
    /// Gets or sets the user who last solved the ticket.
    /// </summary>
    public string SolvedById { get; set; }

    /// <summary>
    /// Gets or sets when the ticket was last given to its assignee.
    /// </summary>
    public DateTime? AssignedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was closed, in UTC.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    // The times and counts the time-elapsed workflows and the reports read without loading each ticket.

    public DateTime? FirstResponseUtc { get; set; }

    public DateTime? StatusChangedUtc { get; set; }

    public DateTime? LastCustomerActivityUtc { get; set; }

    public DateTime? LastAgentActivityUtc { get; set; }

    public int ReplyCount { get; set; }

    public int AgentReplyCount { get; set; }

    public int ReopenCount { get; set; }

    public int EscalationLevel { get; set; }

    /// <summary>
    /// Gets or sets the requester's rating, once given.
    /// </summary>
    public int? SatisfactionScore { get; set; }

    /// <summary>
    /// Gets or sets when the requester was asked to rate the ticket.
    /// </summary>
    public DateTime? RatingRequestedUtc { get; set; }
}
