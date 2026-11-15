using System.Net;
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

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// Tells each approver of the step a ticket waits for, with a link to the approval page.
/// </summary>
internal sealed class TicketApprovalNotifier : ITicketObserver
{
    private readonly UserManager<IUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public TicketApprovalNotifier(
        UserManager<IUser> userManager,
        INotificationService notificationService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<TicketApprovalNotifier> stringLocalizer)
    {
        _userManager = userManager;
        _notificationService = notificationService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task ApprovalRequestedAsync(ContentItem ticket, TicketApprovalStep step, CancellationToken cancellationToken = default)
    {
        var part = ticket.GetOrCreate<TicketPart>();
        var title = $"{part.Number} {ticket.DisplayText}".Trim();
        var encodedTitle = WebUtility.HtmlEncode(title);
        var httpContext = _httpContextAccessor.HttpContext;
        var url = httpContext is null
            ? null
            : _linkGenerator.GetPathByAction(httpContext, "Index", "TicketApprovals", new { area = TicketsConstants.Features.Tickets, id = ticket.ContentItemId });

        foreach (var approverId in step.ApproverIds)
        {
            if (await _userManager.FindByIdAsync(approverId) is not { } user)
            {
                continue;
            }

            await _notificationService.SendAsync(user, new NotificationMessage
            {
                Subject = S["A request waits for your approval: {0}", title],
                Summary = S["A request waits for your approval: {0}.", encodedTitle],
                TextBody = string.IsNullOrEmpty(url) ? S["A request waits for your approval: {0}.", title] : S["A request waits for your approval: {0}: {1}", title, url],
                HtmlBody = string.IsNullOrEmpty(url)
                    ? S["A request waits for your approval: {0}.", encodedTitle]
                    : S["A request waits for your approval: <a href=\"{0}\">{1}</a>.", WebUtility.HtmlEncode(url), encodedTitle],
            }, cancellationToken);
        }
    }
}
