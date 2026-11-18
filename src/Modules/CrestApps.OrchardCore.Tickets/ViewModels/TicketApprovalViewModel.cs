using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.ViewModels;

/// <summary>
/// A ticket's approval, on the ticket page or the approval page.
/// </summary>
public class TicketApprovalViewModel
{
    /// <summary>
    /// Gets or sets the ticket.
    /// </summary>
    public string TicketId { get; set; }

    /// <summary>
    /// Gets or sets the approval.
    /// </summary>
    public TicketApproval Approval { get; set; }

    /// <summary>
    /// Gets or sets whether the user may decide the step the ticket waits for.
    /// </summary>
    public bool CanDecide { get; set; }

    /// <summary>
    /// Gets or sets whether the user may cancel the pending approval.
    /// </summary>
    public bool CanCancel { get; set; }

    /// <summary>
    /// Gets or sets the ticket, on the approval page.
    /// </summary>
    public ContentItem Ticket { get; set; }

    /// <summary>
    /// Gets or sets the requester's name, on the approval page.
    /// </summary>
    public string RequesterName { get; set; }

    /// <summary>
    /// Gets or sets the request's first message, on the approval page.
    /// </summary>
    public string FirstMessage { get; set; }

    /// <summary>
    /// Gets or sets whether the user may open the ticket itself.
    /// </summary>
    public bool CanOpenTicket { get; set; }
}
