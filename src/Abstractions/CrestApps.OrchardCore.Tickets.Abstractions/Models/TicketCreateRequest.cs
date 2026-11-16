using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A new ticket: what was asked, by whom, and where it goes. Empty values come from the ticket type and the desk.
/// </summary>
public sealed class TicketCreateRequest
{
    /// <summary>
    /// Gets or sets the ticket type, a content type with the ticket part; empty uses the default ticket type.
    /// </summary>
    public string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; }

    /// <summary>
    /// Gets or sets what was asked, the first reply on the ticket.
    /// </summary>
    public string Body { get; set; }

    /// <summary>
    /// Gets or sets the files of the first reply.
    /// </summary>
    public List<string> AttachmentIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the id the first reply's timeline entry gets, when its files were stored first as files of that
    /// entry. Leave empty for a new id.
    /// </summary>
    public string EntryId { get; set; }

    /// <summary>
    /// Gets or sets a new, unsaved item of the ticket type whose fields an editor already filled, or
    /// <see langword="null"/> for an empty one. Its content type wins over <see cref="ContentType"/>, and its display
    /// text is the subject when <see cref="Subject"/> is empty.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string PriorityId { get; set; }

    /// <summary>
    /// Gets or sets how much of the business the ticket touches. With <see cref="Urgency"/>, it picks the priority from
    /// the desk's priority matrix when the request names no priority.
    /// </summary>
    public TicketLevel? Impact { get; set; }

    /// <summary>
    /// Gets or sets how soon it must be dealt with.
    /// </summary>
    public TicketLevel? Urgency { get; set; }

    /// <summary>
    /// Gets or sets when work is planned to start, in UTC.
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

    /// <summary>
    /// Gets or sets who asked.
    /// </summary>
    public PartyRef Requester { get; set; }

    /// <summary>
    /// Gets or sets how to reach the requester; empty asks the party directory.
    /// </summary>
    public ContactInfo RequesterContact { get; set; }

    /// <summary>
    /// Gets or sets the account the ticket is for.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the team; empty uses the type's, then the desk's.
    /// </summary>
    public string TeamId { get; set; }

    /// <summary>
    /// Gets or sets the assignee; empty leaves it to assignment.
    /// </summary>
    public string AssigneeId { get; set; }

    /// <summary>
    /// Gets or sets where it came in, such as <c>Email</c> or <c>Portal</c>.
    /// </summary>
    public string Channel { get; set; }

    /// <summary>
    /// Gets or sets who opened it, when not the requester, such as an agent taking a call.
    /// </summary>
    public PartyRef CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the closed ticket this one follows up.
    /// </summary>
    public string FollowUpOfId { get; set; }

    /// <summary>
    /// Gets or sets whether the ticket is brought in from elsewhere (a migration, a bulk import of past tickets): it is
    /// created without telling any observer, so no requester is e-mailed, no workflow starts and no webhook is sent.
    /// </summary>
    public bool IsImport { get; set; }

    /// <summary>
    /// Gets or sets whether the requester's e-mail address is only what they typed, unconfirmed, such as a guest's on
    /// a web form. Such a ticket is never filed under an account by the address's domain, and e-mails to the address
    /// carry none of the text the requester wrote (see <see cref="TicketPart.RequesterUnverified"/>).
    /// </summary>
    public bool RequesterUnverified { get; set; }
}
