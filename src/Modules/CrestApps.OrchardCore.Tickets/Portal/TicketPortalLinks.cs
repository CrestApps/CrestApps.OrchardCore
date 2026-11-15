using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Portal;

/// <summary>
/// The link in a requester's e-mails: their request in My account when they have a login, otherwise a signed link that
/// opens the request without one.
/// </summary>
internal sealed class TicketPortalLinks : ITicketRequesterLinks
{
    private readonly SiteUrlGenerator _urls;
    private readonly TicketPortalAccess _access;

    public TicketPortalLinks(SiteUrlGenerator urls, TicketPortalAccess access)
    {
        _urls = urls;
        _access = access;
    }

    public async Task<string> GetLinkAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        if (ticket?.TryGet<TicketPart>(out var part) != true)
        {
            return null;
        }

        return part.Requester.Kind == PartyKinds.User
            ? await _urls.GetUrlAsync(TicketPortalController.DisplayRouteName, new { number = part.Number })
            : await _urls.GetUrlAsync(TicketPortalController.GuestRouteName, new { id = ticket.ContentItemId, token = _access.CreateGuestToken(ticket.ContentItemId) });
    }
}
