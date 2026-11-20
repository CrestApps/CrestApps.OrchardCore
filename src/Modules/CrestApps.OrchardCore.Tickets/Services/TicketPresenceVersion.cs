using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// A short value that changes whenever a ticket changes, so an open page can tell it is out of date.
/// </summary>
public static class TicketPresenceVersion
{
    /// <summary>
    /// Returns the ticket's version.
    /// </summary>
    /// <param name="ticket">The ticket.</param>
    /// <returns>The version.</returns>
    public static string Of(ContentItem ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var part = ticket.GetOrCreate<TicketPart>();

        return string.Join('.', (ticket.ModifiedUtc ?? ticket.CreatedUtc ?? default).Ticks, part.ReplyCount, part.StatusId, part.AssigneeId);
    }
}
