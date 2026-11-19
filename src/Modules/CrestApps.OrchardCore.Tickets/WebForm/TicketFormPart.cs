using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.WebForm;

/// <summary>
/// A form on a page that opens a ticket: of which type, on which desk, and what the visitor reads once it is sent.
/// </summary>
public sealed class TicketFormPart : ContentPart
{
    /// <summary>
    /// Gets or sets the ticket type the form opens.
    /// </summary>
    public string TicketType { get; set; }

    /// <summary>
    /// Gets or sets the desk the ticket goes to.
    /// </summary>
    public string DeskId { get; set; }

    /// <summary>
    /// Gets or sets what the visitor reads once the request is sent; empty for the default.
    /// </summary>
    public string SuccessMessage { get; set; }
}
