using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Parties.Models;

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
    /// Gets or sets the desk.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public string PriorityId { get; set; }

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
}
