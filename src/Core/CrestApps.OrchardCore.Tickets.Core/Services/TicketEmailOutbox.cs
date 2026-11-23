using CrestApps.Core.Support;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Sends the requester messages of tickets. A requester with a site account gets one through the site's notifications
/// (<see cref="ITicketRequesterNotifier"/>), written by the site's notification template; anyone else, or a requester
/// whose account is gone, gets it by e-mail through the durable templated e-mail outbox (<see cref="ITemplatedEmailOutbox"/>),
/// which tries a failed e-mail again after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours, then gives it up. A
/// reply's entry on the ticket says when its message couldn't be delivered (<see cref="TicketEmailDeliveryObserver"/>),
/// so the agent doesn't believe the requester got an answer they never saw.
/// </summary>
public sealed class TicketEmailOutbox
{
    /// <summary>
    /// How many e-mails kept by an earlier version one run of <see cref="HandOverAsync"/> hands to the templated e-mail
    /// outbox.
    /// </summary>
    public const int SendsPerRun = 50;

    /// <summary>
    /// The property of a requester e-mail (<see cref="TemplatedEmail.Properties"/>) that names the timeline entry it
    /// carries, such as a reply.
    /// </summary>
    public const string EntryIdProperty = "Tickets.EntryId";

    private readonly ISession _session;
    private readonly ITemplatedEmailOutbox _outbox;
    private readonly ITicketRequesterNotifier _notifier;
    private readonly ITemplatedEmailTemplates _templates;
    private readonly ITimelineService _timeline;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailOutbox"/> class.
    /// </summary>
    public TicketEmailOutbox(
        ISession session,
        ITemplatedEmailOutbox outbox,
        IEnumerable<ITicketRequesterNotifier> notifiers,
        IEnumerable<ITemplatedEmailTemplates> templates,
        ITimelineService timeline,
        ILogger<TicketEmailOutbox> logger,
        IStringLocalizer<TicketEmailOutbox> stringLocalizer)
    {
        _session = session;
        _outbox = outbox;
        _notifier = notifiers.LastOrDefault();
        _templates = templates.LastOrDefault();
        _timeline = timeline;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <summary>
    /// Sends a requester message: as a notification to a requester with a site account, by e-mail otherwise.
    /// </summary>
    /// <param name="item">The message.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it reached the requester now; an e-mail that didn't is tried again later.</returns>
    public async Task<bool> SendAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        // A requester with an account hears of it through the site's notifications; one whose account is gone, or anyone
        // without one, by e-mail.
        if (await NotifyAsync(item, cancellationToken))
        {
            await TicketEmailDeliveryObserver.MarkEntryAsync(_timeline, item.EntryId, null, cancellationToken);

            return true;
        }

        var result = await _outbox.SendAsync(Email(item), _ => Task.FromResult(new TemplatedEmailContent { Subject = item.Subject, Html = item.Html }), cancellationToken);

        // The outbox's observer marks the entry of an e-mail it keeps; one it skipped is never tried again.
        if (result.Status == TemplatedEmailOutboxStatus.Skipped)
        {
            await TicketEmailDeliveryObserver.MarkEntryAsync(
                _timeline,
                item.EntryId,
                S["Not delivered to the requester: {0}", S["The site can't send e-mail, or the requester has no e-mail address."]],
                cancellationToken);
        }

        return result.Status == TemplatedEmailOutboxStatus.Sent;
    }

    /// <summary>
    /// Hands the requester e-mails an earlier version kept in the service desk's own outbox to the templated e-mail
    /// outbox: a waiting one is sent again through it, and every one, waiting, sent or given up, is removed.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many e-mails it took; fewer than <see cref="SendsPerRun"/> when none is left.</returns>
    public async Task<int> HandOverAsync(CancellationToken cancellationToken = default)
    {
        var items = await _session.Query<TicketEmailOutboxItem>(TicketsConstants.CollectionName)
            .Take(SendsPerRun)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var item in items)
        {
            count++;
            _session.Delete(item, TicketsConstants.CollectionName);

            if (item.Status == TicketEmailOutboxStatus.Pending)
            {
                await SendAsync(item, cancellationToken);
            }
        }

        return count;
    }

    // The message as the site's template writes it, sent as a notification; false when there's nobody to notify.
    private async Task<bool> NotifyAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.RecipientUserId) || _notifier is null)
        {
            return false;
        }

        try
        {
            var email = Email(item);
            var message = _templates is null
                ? new TemplatedEmailMessage(item.Subject, null, item.Html)
                : await _templates.ApplyAsync(new TemplatedEmailContext
                {
                    Kind = email.Kind,
                    Models = email.Models,
                    Subject = item.Subject,
                    Html = item.Html,
                    Values = new Dictionary<string, string>(),
                }, cancellationToken);

            return await _notifier.NotifyAsync(item.RecipientUserId, item, message, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The requester still gets the message, by e-mail, where the e-mail outbox keeps trying it.
            _logger.LogWarning(exception, "The {Kind} notification of ticket {Number} couldn't be sent; it goes by e-mail.", item.Kind, item.TicketNumber.SanitizeLogValue());

            return false;
        }
    }

    private static TemplatedEmail Email(TicketEmailOutboxItem item)
    {
        var models = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["Ticket"] = new TicketEmailModel { Number = item.TicketNumber, Subject = item.TicketSubject, Status = item.TicketStatus, RequesterName = item.RequesterName, Link = item.TicketLink, Category = item.TicketCategory },
        };

        if (item.ReplyBody is not null || item.ReplyHtml is not null)
        {
            models["Reply"] = new TicketEmailReply { Body = item.ReplyBody, Html = item.ReplyHtml, AuthorName = item.ReplyAuthorName };
        }

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(item.EntryId))
        {
            properties[EntryIdProperty] = item.EntryId;
        }

        return new TemplatedEmail
        {
            Kind = item.Kind,
            To = item.To,
            Models = models,
            Reference = item.Reference,
            Source = string.IsNullOrEmpty(item.TicketId) ? default : new RecordRef(TicketsConstants.RecordKind, item.TicketId),
            Properties = properties,
        };
    }
}
