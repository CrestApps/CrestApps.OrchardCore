using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Finds where a ticket's requester is e-mailed: nowhere on a desk that doesn't e-mail requesters, else the e-mail kept on
/// the ticket (a guest's), else the requester's own.
/// </summary>
public static class TicketRequesterEmail
{
    /// <summary>
    /// Returns the requester's e-mail address, or <see langword="null"/> when they aren't e-mailed.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <param name="desk">Its desk.</param>
    /// <param name="parties">The party directory.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    public static async Task<string> FindAsync(TicketPart part, TicketDesk desk, IPartyDirectory parties, CancellationToken cancellationToken = default)
    {
        if (part is null || desk is not { NotifyRequester: true })
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(part.RequesterContact?.Email))
        {
            return part.RequesterContact.Email.Trim();
        }

        return part.Requester.IsEmpty || parties is null ? null : (await parties.GetContactInfoAsync(part.Requester, cancellationToken))?.Email;
    }
}
