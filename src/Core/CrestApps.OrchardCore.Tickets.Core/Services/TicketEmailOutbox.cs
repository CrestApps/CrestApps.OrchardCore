using CrestApps.Core.Support;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Timeline;
using CrestApps.OrchardCore.Timeline.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tickets.Core.Services;

/// <summary>
/// Sends the requester messages of tickets and keeps what happened: a message is stored, tried at once, and tried again
/// later when it fails (after 5 minutes, 15 minutes, an hour, 4 hours and 12 hours), then given up. A requester with a
/// site account gets it through the site's notifications (<see cref="ITicketRequesterNotifier"/>), written by the
/// site's notification template; anyone else gets it by e-mail. A reply's entry on the ticket says when its message
/// couldn't be delivered, so the agent doesn't believe the requester got an answer they never saw.
/// </summary>
public sealed class TicketEmailOutbox
{
    /// <summary>
    /// How many e-mails one run tries.
    /// </summary>
    public const int SendsPerRun = 50;

    private static readonly TimeSpan[] _retries = [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromHours(1), TimeSpan.FromHours(4), TimeSpan.FromHours(12)];

    private readonly ISession _session;
    private readonly ITemplatedEmailSender _sender;
    private readonly ITicketRequesterNotifier _notifier;
    private readonly ITemplatedEmailTemplates _templates;
    private readonly ITimelineService _timeline;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TicketEmailOutbox"/> class.
    /// </summary>
    public TicketEmailOutbox(
        ISession session,
        ITemplatedEmailSender sender,
        IEnumerable<ITicketRequesterNotifier> notifiers,
        IEnumerable<ITemplatedEmailTemplates> templates,
        ITimelineService timeline,
        IClock clock,
        ILogger<TicketEmailOutbox> logger,
        IStringLocalizer<TicketEmailOutbox> stringLocalizer)
    {
        _logger = logger;
        _session = session;
        _sender = sender;
        _notifier = notifiers.LastOrDefault();
        _templates = templates.LastOrDefault();
        _timeline = timeline;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Stores an e-mail and tries it now.
    /// </summary>
    /// <param name="item">The e-mail.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Whether it was sent.</returns>
    public async Task<bool> SendAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.CreatedUtc = _clock.UtcNow;
        item.Status = TicketEmailOutboxStatus.Pending;

        return await TryAsync(item, cancellationToken);
    }

    /// <summary>
    /// Tries the e-mails whose next attempt is due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were sent.</returns>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var due = await _session.Query<TicketEmailOutboxItem, TicketEmailOutboxIndex>(index => index.Status == TicketEmailOutboxStatus.Pending && index.NextAttemptUtc != null && index.NextAttemptUtc <= now, TicketsConstants.CollectionName)
            .OrderBy(index => index.NextAttemptUtc)
            .Take(SendsPerRun)
            .ListAsync(cancellationToken);
        var sent = 0;

        foreach (var item in due)
        {
            if (await TryAsync(item, cancellationToken))
            {
                sent++;
            }
        }

        return sent;
    }

    private async Task<bool> TryAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        bool sent;

        item.Attempts++;

        try
        {
            // A requester with an account hears of it through the site's notifications; one whose account is gone, or
            // anyone without one, by e-mail.
            sent = await NotifyAsync(item, cancellationToken);

            if (sent)
            {
                item.LastError = null;
            }
            else
            {
                sent = await _sender.SendAsync(Email(item), _ => Task.FromResult(new TemplatedEmailContent { Subject = item.Subject, Html = item.Html }), cancellationToken);
                item.LastError = sent ? null : S["The e-mail service didn't take it; check that the site's e-mail is set up."];
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            sent = false;
            item.LastError = exception.Message;
        }

        if (sent)
        {
            item.Status = TicketEmailOutboxStatus.Sent;
            item.SentUtc = now;
            item.NextAttemptUtc = null;
        }
        else if (item.Attempts > _retries.Length)
        {
            item.Status = TicketEmailOutboxStatus.Abandoned;
            item.NextAttemptUtc = null;

            // Shown on the reply as "not delivered", and here for whoever watches the logs.
            _logger.LogWarning("The {Kind} e-mail of ticket {Number} wasn't delivered after {Attempts} attempts: {Error}", item.Kind, item.TicketNumber.SanitizeLogValue(), item.Attempts, item.LastError.SanitizeLogValue());
        }
        else
        {
            item.NextAttemptUtc = now.Add(_retries[item.Attempts - 1]);
        }

        await _session.SaveAsync(item, false, TicketsConstants.CollectionName, cancellationToken);
        await MarkEntryAsync(item, cancellationToken);

        return sent;
    }

    // The message as the site's template writes it, sent as a notification; false when there's nobody to notify.
    private async Task<bool> NotifyAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.RecipientUserId) || _notifier is null)
        {
            return false;
        }

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

    // The reply's entry shows a notice while its e-mail hasn't reached the requester.
    private async Task MarkEntryAsync(TicketEmailOutboxItem item, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(item.EntryId))
        {
            return;
        }

        string notice = item.Status switch
        {
            TicketEmailOutboxStatus.Sent => null,
            TicketEmailOutboxStatus.Abandoned => S["Not delivered to the requester: {0}", item.LastError],
            _ => S["Not delivered yet; trying again. {0}", item.LastError],
        };

        await _timeline.UpdateAsync(item.EntryId, changes =>
        {
            if (notice is null)
            {
                changes.Properties.Remove(TimelineConstants.Properties.Notice);
            }
            else
            {
                changes.Properties[TimelineConstants.Properties.Notice] = notice;
            }
        }, cancellationToken);
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

        return new TemplatedEmail { Kind = item.Kind, To = item.To, Models = models, Reference = item.Reference };
    }
}
