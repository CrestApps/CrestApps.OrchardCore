using System.Net;
using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tickets.Services;

/// <summary>
/// Tells an agent, through Orchard's notifications (the notification center and the user's notification methods), that a
/// ticket was given to them, on a desk that tells assignees. Nobody is told about a ticket they took themselves.
/// </summary>
internal sealed class TicketAssignmentNotifier : ITicketObserver
{
    private readonly ICatalog<TicketDesk> _desks;
    private readonly UserManager<IUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public TicketAssignmentNotifier(
        ICatalog<TicketDesk> desks,
        UserManager<IUser> userManager,
        INotificationService notificationService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<TicketAssignmentNotifier> stringLocalizer)
    {
        _desks = desks;
        _userManager = userManager;
        _notificationService = notificationService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task AssignedAsync(ContentItem ticket, string previousAssigneeId, CancellationToken cancellationToken = default)
    {
        if (!ticket.TryGet<TicketPart>(out var part) || string.IsNullOrEmpty(part.AssigneeId))
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        if (string.Equals(httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier), part.AssigneeId, StringComparison.Ordinal)
            || await _desks.FindByIdAsync(part.DeskId, cancellationToken) is not { NotifyAssignee: true }
            || await _userManager.FindByIdAsync(part.AssigneeId) is not { } user)
        {
            return;
        }

        var title = $"{part.Number} {ticket.DisplayText}".Trim();
        var encodedTitle = WebUtility.HtmlEncode(title);
        var url = httpContext is null
            ? null
            : _linkGenerator.GetPathByAction(httpContext, "Display", "Tickets", new { area = TicketsConstants.Features.Tickets, id = ticket.ContentItemId });

        await _notificationService.SendAsync(user, new NotificationMessage
        {
            Subject = S["A ticket was assigned to you: {0}", title],
            Summary = S["A ticket was assigned to you: {0}.", encodedTitle],
            TextBody = string.IsNullOrEmpty(url) ? S["A ticket was assigned to you: {0}.", title] : S["A ticket was assigned to you: {0}: {1}", title, url],
            HtmlBody = string.IsNullOrEmpty(url)
                ? S["A ticket was assigned to you: {0}.", encodedTitle]
                : S["A ticket was assigned to you: <a href=\"{0}\">{1}</a>.", WebUtility.HtmlEncode(url), encodedTitle],
        }, cancellationToken);
    }
}
