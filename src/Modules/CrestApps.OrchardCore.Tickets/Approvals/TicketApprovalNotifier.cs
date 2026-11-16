using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Notifications;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using OrchardCore.Admin;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tickets.Approvals;

/// <summary>
/// Tells each approver of the step a ticket waits for, with a link to the approval page.
/// </summary>
internal sealed class TicketApprovalNotifier : ITicketObserver
{
    private readonly TicketNotificationSender _sender;
    private readonly AdminOptions _adminOptions;

    internal readonly IStringLocalizer S;

    public TicketApprovalNotifier(
        TicketNotificationSender sender,
        IOptions<AdminOptions> adminOptions,
        IStringLocalizer<TicketApprovalNotifier> stringLocalizer)
    {
        _sender = sender;
        _adminOptions = adminOptions.Value;
        S = stringLocalizer;
    }

    public Task ApprovalRequestedAsync(ContentItem ticket, TicketApprovalStep step, CancellationToken cancellationToken = default)
        => _sender.SendAsync(
            step.ApproverIds,
            ticket,
            TicketsConstants.NotificationTypes.ApprovalRequested,
            title => S["A request waits for your approval: {0}", title],
            url: $"~/{_adminOptions.AdminUrlPrefix}/ticket/{ticket.ContentItemId}/approval",
            cancellationToken: cancellationToken);
}
