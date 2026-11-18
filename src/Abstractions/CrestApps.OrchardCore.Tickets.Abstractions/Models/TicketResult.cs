using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Models;

/// <summary>
/// What a change to a ticket did: the ticket, or why it was refused.
/// </summary>
public sealed class TicketResult
{
    /// <summary>
    /// Gets the ticket.
    /// </summary>
    public ContentItem Ticket { get; init; }

    /// <summary>
    /// Gets why the change was refused.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Gets whether the change was made.
    /// </summary>
    public bool Succeeded => Errors.Count == 0;
}
