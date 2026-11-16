using System.Text.Encodings.Web;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Parties.Services;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Asks requesters to rate their solved tickets: on each desk with a rating delay, the tickets solved that long ago (and
/// no longer than a week before that, so turning it on doesn't e-mail every old ticket) whose requester wasn't asked yet,
/// once each, through the e-mail outbox.
/// </summary>
public sealed class TicketRatingRequests
{
    /// <summary>
    /// How many requests one run sends per desk.
    /// </summary>
    public const int PerDeskPerRun = 50;

    // A ticket solved longer ago than its desk's delay plus this isn't asked any more.
    private static readonly TimeSpan _window = TimeSpan.FromDays(7);

    private static readonly TicketStatusCategory[] _finished = [TicketStatusCategory.Solved, TicketStatusCategory.Closed];

    private readonly ISession _session;
    private readonly ICatalog<TicketDesk> _desks;
    private readonly ITicketService _tickets;
    private readonly IPartyDirectory _parties;
    private readonly TicketEmailOutbox _outbox;
    private readonly IEnumerable<ITicketRequesterLinks> _links;
    private readonly IClock _clock;
    private readonly ITicketCategoryService _categories;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketRatingRequests"/> class.
    /// </summary>
    public TicketRatingRequests(
        ISession session,
        ICatalog<TicketDesk> desks,
        ITicketService tickets,
        IPartyDirectory parties,
        TicketEmailOutbox outbox,
        IEnumerable<ITicketRequesterLinks> links,
        IClock clock,
        IEnumerable<ITicketCategoryService> categories,
        IStringLocalizer<TicketRatingRequests> stringLocalizer)
    {
        _session = session;
        _desks = desks;
        _tickets = tickets;
        _parties = parties;
        _outbox = outbox;
        _links = links;
        _clock = clock;
        _categories = categories.FirstOrDefault();
        S = stringLocalizer;
    }

    /// <summary>
    /// Sends the rating requests that are due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many requesters were asked.</returns>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        // The rating page is the portal's; without it there is nowhere to send people.
        if (_links.FirstOrDefault() is not { } links)
        {
            return 0;
        }

        var now = _clock.UtcNow;
        var sent = 0;

        foreach (var desk in await _desks.GetAllAsync(cancellationToken))
        {
            if (desk.RatingRequestDelayHours <= 0 || !desk.NotifyRequester)
            {
                continue;
            }

            var deskId = desk.ItemId;
            var cutoff = now.AddHours(-desk.RatingRequestDelayHours);
            var oldest = cutoff - _window;
            var due = await _session.Query<ContentItem, TicketIndex>(index => index.DeskId == deskId
                    && index.RatingRequestedUtc == null
                    && index.SatisfactionScore == null
                    && index.MergedIntoId == null
                    && index.StatusCategory.IsIn(_finished)
                    && index.SolvedUtc != null && index.SolvedUtc <= cutoff && index.SolvedUtc >= oldest)
                .OrderBy(index => index.SolvedUtc)
                .Take(PerDeskPerRun)
                .ListAsync(cancellationToken);

            foreach (var ticket in due)
            {
                var part = ticket.GetOrCreate<TicketPart>();
                var to = await TicketRequesterEmail.FindAsync(part, desk, _parties, cancellationToken);
                var link = string.IsNullOrEmpty(to) ? null : await links.GetRatingLinkAsync(ticket, cancellationToken);

                // Marked first, so a request is never sent twice; one that can't be sent (no address, no site link) is
                // marked too and not tried again.
                await _tickets.RecordRatingRequestAsync(ticket.ContentItemId, cancellationToken);

                if (string.IsNullOrEmpty(link))
                {
                    continue;
                }

                // In a rating request, Ticket.Link is the rating page.
                var model = TicketEmailModel.From(ticket, null, link, await TicketEmailModel.CategoryAsync(ticket, _categories, cancellationToken));
                var encoder = HtmlEncoder.Default;
                var html = $"<p>{encoder.Encode(S["How did we do with your request {0}?", model.Number].Value)}</p>"
                    + $"<p><strong>{encoder.Encode(model.Subject ?? string.Empty)}</strong></p>"
                    + $"<p><a href=\"{encoder.Encode(link)}\">{encoder.Encode(S["Rate how we handled it"].Value)}</a></p>";

                await _outbox.SendAsync(new TicketEmailOutboxItem
                {
                    TicketId = ticket.ContentItemId,
                    Kind = TicketsConstants.EmailKinds.Rating,
                    To = to,
                    Reference = model.Number,
                    Subject = S["How did we do? {0}", model.Subject].Value,
                    Html = html,
                    TicketNumber = model.Number,
                    TicketSubject = model.Subject,
                    TicketCategory = model.Category,
                    RequesterName = model.RequesterName,
                    TicketLink = link,
                }, cancellationToken);

                sent++;
            }
        }

        return sent;
    }
}
