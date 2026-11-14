using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// Makes a content item a ticket. Every change goes through <c>ITicketService</c>, which keeps these values, the
/// ticket's timeline and its service-level timers in step.
/// </summary>
public sealed class TicketPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ticket number people quote, such as <c>T-1001</c>.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the desk the ticket belongs to.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the ticket's status.
    /// </summary>
    public string StatusId { get; set; }

    /// <summary>
    /// Gets or sets the category of the ticket's status.
    /// </summary>
    public TicketStatusCategory StatusCategory { get; set; }

    /// <summary>
    /// Gets or sets the ticket's priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets the priority's rank when it was set, for sorting by urgency.
    /// </summary>
    public int PriorityRank { get; set; }

    /// <summary>
    /// Gets or sets who asked: a user, a customer, a contact or a guest.
    /// </summary>
    public PartyRef Requester { get; set; }

    /// <summary>
    /// Gets or sets how to reach the requester when the ticket was opened.
    /// </summary>
    public ContactInfo RequesterContact { get; set; }

    /// <summary>
    /// Gets or sets the account the ticket is for, a content item id.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the team that works the ticket.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the user the ticket is assigned to.
    /// </summary>
    public string AssigneeId { get; set; }

    /// <summary>
    /// Gets or sets where the ticket came in, such as <c>Email</c> or <c>Portal</c>; empty for the admin.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets the people copied on the ticket and those who follow it.
    /// </summary>
    public List<PartyRef> Participants { get; set; } = [];

    /// <summary>
    /// Gets or sets the records the ticket is linked to.
    /// </summary>
    public List<TicketLink> Links { get; set; } = [];

    /// <summary>
    /// Gets or sets the ticket this one was merged into.
    /// </summary>
    public string MergedIntoId { get; set; }

    /// <summary>
    /// Gets or sets the closed ticket this one follows up.
    /// </summary>
    public string FollowUpOfId { get; set; }

    /// <summary>
    /// Gets or sets when the first public agent reply was sent, in UTC.
    /// </summary>
    public DateTime? FirstResponseUtc { get; set; }

    /// <summary>
    /// Gets or sets when the ticket was last solved, in UTC.
    /// </summary>
    public DateTime? SolvedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the ticket was closed, in UTC.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the status last changed, in UTC.
    /// </summary>
    public DateTime? StatusChangedUtc { get; set; }

    /// <summary>
    /// Gets or sets when the requester last wrote, in UTC.
    /// </summary>
    public DateTime? LastCustomerActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets when an agent last replied, in UTC.
    /// </summary>
    public DateTime? LastAgentActivityUtc { get; set; }

    /// <summary>
    /// Gets or sets how many public replies the ticket has.
    /// </summary>
    public int ReplyCount { get; set; }

    /// <summary>
    /// Gets or sets how many times the ticket was reopened.
    /// </summary>
    public int ReopenCount { get; set; }

    /// <summary>
    /// Gets or sets how many times the ticket changed hands.
    /// </summary>
    public int ReassignmentCount { get; set; }

    /// <summary>
    /// Gets or sets how far the ticket was escalated; 0 when it wasn't.
    /// </summary>
    public int EscalationLevel { get; set; }

    /// <summary>
    /// Gets or sets the earliest due time of the ticket's running service-level timers, in UTC, for lists and views.
    /// </summary>
    public DateTime? NextDueUtc { get; set; }

    /// <summary>
    /// Gets or sets when the ticket was last given to its assignee, so team assignment can take turns.
    /// </summary>
    public DateTime? AssignedUtc { get; set; }
}
