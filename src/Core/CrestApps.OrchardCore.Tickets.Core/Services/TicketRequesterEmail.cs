using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tickets.Models;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Finds where a ticket's requester is told about it: nowhere on a desk that doesn't notify requesters, else their site
/// account (when they have one) and the e-mail kept on the ticket (a guest's), else the requester's own.
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

    /// <summary>
    /// Returns where the requester is told: their site account, notified through the site's notifications, and their
    /// e-mail address, used when they have no account (or it is gone). <see langword="null"/> when they have neither, or
    /// the desk doesn't notify requesters.
    /// </summary>
    /// <param name="part">The ticket.</param>
    /// <param name="desk">Its desk.</param>
    /// <param name="parties">The party directory.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The recipient, or <see langword="null"/>.</returns>
    public static async Task<TicketRequesterRecipient> FindRecipientAsync(TicketPart part, TicketDesk desk, IPartyDirectory parties, CancellationToken cancellationToken = default)
    {
        if (part is null || desk is not { NotifyRequester: true })
        {
            return null;
        }

        var userId = part.Requester.Kind == PartyKinds.User && !string.IsNullOrEmpty(part.Requester.Id) ? part.Requester.Id : null;
        var email = await FindAsync(part, desk, parties, cancellationToken);

        return userId is null && string.IsNullOrEmpty(email) ? null : new TicketRequesterRecipient(userId, email);
    }
}

/// <summary>
/// Where a requester is told about their ticket.
/// </summary>
/// <param name="UserId">Their user id, when they have a site account.</param>
/// <param name="Email">Their e-mail address, if known.</param>
public sealed record TicketRequesterRecipient(string UserId, string Email);
