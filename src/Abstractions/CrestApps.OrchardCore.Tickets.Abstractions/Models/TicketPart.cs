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
    /// Gets or sets the requester's rating, once given.
    /// </summary>
    public TicketSatisfaction Satisfaction { get; set; }

    /// <summary>
    /// Gets or sets when the requester was asked to rate the ticket, so they are asked once.
    /// </summary>
    public DateTime? RatingRequestedUtc { get; set; }

    /// <summary>
    /// Gets or sets whether the requester's e-mail address is unconfirmed (a guest typed it): e-mails to it carry only
    /// fixed text and the ticket's number, never the subject, name or link the guest's words would fill, so the site
    /// can't be used to send someone else's text to any address.
    /// </summary>
    public bool RequesterUnverified { get; set; }

    /// <summary>
    /// Gets or sets the version of the ticket's guest and rating links: raising it (on a merge, an erasure) makes every
    /// link sent before stop working.
    /// </summary>
    public int AccessVersion { get; set; }

    /// <summary>
    /// Gets or sets the ticket's approval, when its type asks for one.
    /// </summary>
    public TicketApproval Approval { get; set; }

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
    /// Gets or sets how many public replies agents sent themselves (not automated ones).
    /// </summary>
    public int AgentReplyCount { get; set; }

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

    /// <summary>
    /// Gets or sets the category: a term of the taxonomy the ticket type names
    /// (<see cref="TicketPartSettings.CategoryTaxonomyId"/>), or none.
    /// </summary>
    public string CategoryId { get; set; }

    /// <summary>
    /// Gets or sets the category and the categories above it as ids, top first, written <c>/top/child/category/</c>, so
    /// a filter on a category finds its sub-categories too. Kept with <see cref="CategoryId"/>.
    /// </summary>
    public string CategoryPath { get; set; }

    /// <summary>
    /// Gets or sets the user who last solved the ticket, when a person did.
    /// </summary>
    public string SolvedById { get; set; }

    /// <summary>
    /// Gets or sets how much of the business the ticket touches.
    /// </summary>
    public TicketLevel? Impact { get; set; }

    /// <summary>
    /// Gets or sets how soon it must be dealt with.
    /// </summary>
    public TicketLevel? Urgency { get; set; }

    /// <summary>
    /// Gets or sets when work on it is planned to start, in UTC.
    /// </summary>
    public DateTime? EstimatedStartUtc { get; set; }

    /// <summary>
    /// Gets or sets how long the work is expected to take, in minutes.
    /// </summary>
    public int? EstimatedMinutes { get; set; }

    /// <summary>
    /// Gets or sets how the work is charged.
    /// </summary>
    public TicketBillingType? BillingType { get; set; }
}
