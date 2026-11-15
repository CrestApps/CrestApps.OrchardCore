using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.Logging;
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
    private readonly ILogger _logger;

    public TicketPortalLinks(SiteUrlGenerator urls, TicketPortalAccess access, ILogger<TicketPortalLinks> logger)
    {
        _urls = urls;
        _access = access;
        _logger = logger;
    }

    public async Task<string> GetLinkAsync(ContentItem ticket, CancellationToken cancellationToken = default)
    {
        if (ticket?.TryGet<TicketPart>(out var part) != true)
        {
            return null;
        }

        return Logged(part.Requester.Kind == PartyKinds.User
            ? await _urls.GetSiteUrlAsync(TicketPortalController.DisplayRouteName, new { number = part.Number })
            : await _urls.GetSiteUrlAsync(TicketPortalController.GuestRouteName, new { id = ticket.ContentItemId, token = _access.CreateGuestToken(ticket) }));
    }

    public async Task<string> GetRatingLinkAsync(ContentItem ticket, CancellationToken cancellationToken = default)
        => ticket is null
            ? null
            : Logged(await _urls.GetSiteUrlAsync(TicketPortalController.RateRouteName, new { id = ticket.ContentItemId, token = _access.CreateRatingToken(ticket) }));

    // Links in e-mails are built from the site's Base URL only: the request's host is whatever the client sent.
    private string Logged(string url)
    {
        if (url is null)
        {
            _logger.LogWarning("Requester e-mails go out without a link to the request because the site has no Base URL.");
        }

        return url;
    }
}
