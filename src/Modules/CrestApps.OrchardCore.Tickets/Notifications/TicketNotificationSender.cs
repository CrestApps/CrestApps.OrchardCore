using System.Net;
using System.Security.Claims;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;
using OrchardCore.Entities;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Notifications;

/// <summary>
/// Sends the ticket notifications through Orchard Core's notifications, so each user gets them in the notification
/// center and by the methods they chose for the type. Every notification names its type, the ticket (its reference id)
/// and the page it opens, and carries <see cref="TicketNotificationInfo"/> for its display.
/// </summary>
internal sealed class TicketNotificationSender
{
    private readonly UserManager<IUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly INotificationManager _notificationManager;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public TicketNotificationSender(
        UserManager<IUser> userManager,
        INotificationService notificationService,
        INotificationManager notificationManager,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AdminOptions> adminOptions,
        IStringLocalizer<TicketNotificationSender> stringLocalizer)
    {
        _userManager = userManager;
        _notificationService = notificationService;
        _notificationManager = notificationManager;
        _httpContextAccessor = httpContextAccessor;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the user who made the change being raised, when a person made it: the ticket event's actor, or the signed-in
    /// user outside an event.
    /// </summary>
    public string ActorUserId
    {
        get
        {
            if (TicketEventActor.Current is { } actor)
            {
                return actor.Party.Kind == PartyKinds.User ? actor.Party.Id : null;
            }

            return _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }

    /// <summary>
    /// Gets the name of who made the change being raised, when a person made it.
    /// </summary>
    public string ActorName
    {
        get
        {
            if (TicketEventActor.Current is { } actor)
            {
                return actor.Party.Kind == PartyKinds.User ? actor.Name : null;
            }

            return _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        }
    }

    /// <summary>
    /// The page of a ticket in the admin, application-relative, so it works from a background task too.
    /// </summary>
    /// <param name="ticketId">The ticket.</param>
    /// <returns>The URL.</returns>
    public string AdminUrl(string ticketId)
        => $"~/{_adminOptions.AdminUrlPrefix}/ticket/{ticketId}";

    /// <summary>
    /// Tells users about a ticket, each once, skipping who made the change and users that no longer exist.
    /// </summary>
    /// <param name="userIds">The users.</param>
    /// <param name="ticket">The ticket.</param>
    /// <param name="type">The notification type, one of <see cref="TicketsConstants.NotificationTypes"/>.</param>
    /// <param name="subject">The subject, plain text, with <c>{0}</c> where the ticket's number and subject go.</param>
    /// <param name="url">The page it opens; the ticket's admin page when <see langword="null"/>.</param>
    /// <param name="info">Extra details, such as the service-level metric.</param>
    /// <param name="skip">Tells whether a user already has this notification (unread), so it isn't sent twice.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many were sent.</returns>
    /// <summary>
    /// Sends a notification already written to one user.
    /// </summary>
    /// <param name="userId">The user.</param>
    /// <param name="message">The notification.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns><see langword="false"/> when the user can't be found.</returns>
    public async Task<bool> SendToUserAsync(string userId, NotificationMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrEmpty(userId) || await _userManager.FindByIdAsync(userId) is not { } user)
        {
            return false;
        }

        await _notificationService.SendAsync(user, message, cancellationToken);

        return true;
    }

    public async Task<int> SendAsync(
        IEnumerable<string> userIds,
        ContentItem ticket,
        string type,
        Func<string, LocalizedString> subject,
        string url = null,
        Action<TicketNotificationInfo> info = null,
        Func<Notification, bool> skip = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var part = ticket.GetOrCreate<TicketPart>();
        var title = $"{part.Number} {ticket.DisplayText}".Trim();
        var actorId = ActorUserId;
        var actorName = ActorName;
        url ??= AdminUrl(ticket.ContentItemId);
        var sent = 0;

        foreach (var userId in userIds.Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (string.Equals(userId, actorId, StringComparison.Ordinal)
                || await _userManager.FindByIdAsync(userId) is not { } user)
            {
                continue;
            }

            if (skip is not null)
            {
                var unread = await _notificationManager.ListAsync(new NotificationFilter { UserId = userId, Type = type, ReferenceId = ticket.ContentItemId, IsRead = false }, cancellationToken);

                if (unread.Any(skip))
                {
                    continue;
                }
            }

            var text = subject(title).Value;
            var by = string.IsNullOrEmpty(actorName) ? null : S["By {0}.", actorName].Value;
            var message = new NotificationMessage
            {
                Subject = text,
                Summary = by is null ? null : WebUtility.HtmlEncode(by),
                TextBody = by is null ? text : text + Environment.NewLine + by,
                HtmlBody = WebUtility.HtmlEncode(text) + (by is null ? string.Empty : "<br>" + WebUtility.HtmlEncode(by)),
                Url = url,
                Type = type,
                ReferenceId = ticket.ContentItemId,
            };

            var details = new TicketNotificationInfo
            {
                TicketId = ticket.ContentItemId,
                Number = part.Number,
                Title = ticket.DisplayText,
                DeskId = part.DeskId,
                By = actorName,
            };
            info?.Invoke(details);
            message.Put(details);

            await _notificationService.SendAsync(user, message, cancellationToken);
            sent++;
        }

        return sent;
    }
}
