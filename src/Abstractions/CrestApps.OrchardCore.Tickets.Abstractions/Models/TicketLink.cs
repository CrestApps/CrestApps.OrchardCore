using CrestApps.OrchardCore.Records;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// A record a ticket is linked to, such as an order, an asset or another ticket.
/// </summary>
public sealed class TicketLink
{
    /// <summary>
    /// Gets or sets the record.
    /// </summary>
    public RecordRef Record { get; set; }

    /// <summary>
    /// Gets or sets how they relate, such as <c>Related</c>, <c>Parent</c>, <c>Child</c> or <c>Duplicate</c>.
    /// </summary>
    public string Kind { get; set; }
}
