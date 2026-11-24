namespace CrestApps.OrchardCore.Invoicing.AI;

/// <summary>
/// What the invoicing tools know about the current chat message. Every tool call of a message shares one database
/// session, and an invoice command that is refused discards the session's unsaved work
/// (<c>DefaultInvoiceCommandService</c> cancels it), so once that happened nothing more is written in the message.
/// </summary>
internal sealed class InvoicingToolScope
{
    /// <summary>
    /// Gets or sets a value indicating whether the session discarded its work in this message.
    /// </summary>
    public bool SessionClosed { get; set; }
}
