using System.Net;
using CrestApps.OrchardCore.Emails;
using CrestApps.OrchardCore.Tickets.Core.Indexes;
using CrestApps.OrchardCore.Tickets.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Notifications.Models;

namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// Sends the requester messages (received, an agent's reply, solved, the rating request) to a requester who has a site
/// account through Orchard Core's Notifications, with a notification type for each kind, so the requester finds them in
/// the notification center and gets them by the methods they chose (e-mail, SMS...) or turns a kind off. The message is
/// the one the site's notification template wrote, and it opens the request in the requester's account.
/// </summary>
internal sealed class TicketRequesterNotifier : ITicketRequesterNotifier
{
    private const int SummaryLength = 200;

    private readonly TicketNotificationSender _sender;

    internal readonly IStringLocalizer S;

    public TicketRequesterNotifier(TicketNotificationSender sender, IStringLocalizer<TicketRequesterNotifier> stringLocalizer)
    {
        _sender = sender;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the notification type of a kind of requester message.
    /// </summary>
    /// <param name="kind">The kind (<see cref="TicketsConstants.EmailKinds"/>).</param>
    /// <returns>The notification type, or <see langword="null"/> for a kind it doesn't know.</returns>
    public static string TypeOf(string kind)
        => kind switch
        {
            TicketsConstants.EmailKinds.Received => TicketsConstants.NotificationTypes.RequestReceived,
            TicketsConstants.EmailKinds.Reply => TicketsConstants.NotificationTypes.Replied,
            TicketsConstants.EmailKinds.Solved => TicketsConstants.NotificationTypes.RequestSolved,
            TicketsConstants.EmailKinds.Rating => TicketsConstants.NotificationTypes.RatingRequested,
            _ => null,
        };

    /// <inheritdoc/>
    public Task<bool> NotifyAsync(string userId, TicketEmailOutboxItem item, TemplatedEmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(message);

        var notification = new NotificationMessage
        {
            Subject = message.Subject,
            Summary = WebUtility.HtmlEncode(Summary(item)),
            TextBody = message.Text,
            HtmlBody = message.Html,
            IsHtmlPreferred = !string.IsNullOrEmpty(message.Html),

            // The request in the requester's account (or, for a rating request, its rating page).
            Url = string.IsNullOrEmpty(item.TicketLink) ? "~/account/requests/" + Uri.EscapeDataString(item.TicketNumber ?? string.Empty) : item.TicketLink,
            Type = TypeOf(item.Kind) ?? TicketsConstants.NotificationTypes.Replied,
            ReferenceId = item.TicketId,
        };

        notification.Put(new TicketNotificationInfo
        {
            TicketId = item.TicketId,
            Number = item.TicketNumber,
            Title = item.TicketSubject,
            By = item.ReplyAuthorName,
        });

        return _sender.SendToUserAsync(userId, notification, cancellationToken);
    }

    // One line for the notification center: what happened, or the start of the reply.
    private string Summary(TicketEmailOutboxItem item)
        => item.Kind switch
        {
            TicketsConstants.EmailKinds.Received => S["We received your request {0}.", item.TicketNumber],
            TicketsConstants.EmailKinds.Solved => S["Your request {0} is solved.", item.TicketNumber],
            TicketsConstants.EmailKinds.Rating => S["How did we do with your request {0}?", item.TicketNumber],
            _ when string.IsNullOrWhiteSpace(item.ReplyBody) => S["Your request {0} has a new reply.", item.TicketNumber],
            _ => string.IsNullOrEmpty(item.ReplyAuthorName) ? Shorten(item.ReplyBody) : S["{0}: {1}", item.ReplyAuthorName, Shorten(item.ReplyBody)],
        };

    private static string Shorten(string text)
    {
        var line = string.Join(' ', text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries));

        return line.Length <= SummaryLength ? line : string.Concat(line.AsSpan(0, SummaryLength - 1).TrimEnd(), "…");
    }
}
