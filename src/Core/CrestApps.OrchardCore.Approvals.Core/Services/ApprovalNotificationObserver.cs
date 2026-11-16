using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Approvals.Core.Services;

/// <summary>
/// The approver's inbox is Orchard's notification center: a new request notifies each approver (the
/// <see cref="ApprovalsConstants.ApprovalNotificationType"/> type, linking to the request), a decision removes the
/// open notices of the request and tells the requester. No amount is put in a notice.
/// </summary>
public sealed class ApprovalNotificationObserver : IApprovalObserver
{
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="ApprovalNotificationObserver"/> class.
    /// </summary>
    /// <param name="serviceProvider">The services, for the approval rules (which tell this observer, so they are
    /// taken when needed), the notification services and the user store.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public ApprovalNotificationObserver(IServiceProvider serviceProvider, IStringLocalizer<ApprovalNotificationObserver> stringLocalizer)
    {
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <summary>
    /// Gets the page an approver opens a request on, application-relative.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns>The URL.</returns>
    public static string RequestUrl(ApprovalRequest request)
        => "~/Admin/approvals/decide/" + request.ItemId;

    /// <inheritdoc />
    public async Task OnChangedAsync(ApprovalChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var notifications = _serviceProvider.GetService<INotificationService>();
        var userManager = _serviceProvider.GetService<UserManager<IUser>>();

        if (notifications is null || userManager is null)
        {
            return;
        }

        var request = change.Request;

        // A new request, or the next stage of one: its approvers are asked, and the last stage's notices go.
        if (change.Kind is ApprovalChangeKind.Requested or ApprovalChangeKind.StageApproved)
        {
            if (change.Kind == ApprovalChangeKind.StageApproved && _serviceProvider.GetService<INotificationManager>() is { } stageNotices)
            {
                await stageNotices.DeleteAsync(new NotificationFilter
                {
                    Type = ApprovalsConstants.ApprovalNotificationType,
                    ReferenceId = request.ItemId,
                }, cancellationToken);
            }

            foreach (var userId in await _serviceProvider.GetRequiredService<IApprovalService>().GetApproverUserIdsAsync(request, cancellationToken))
            {
                if (await userManager.FindByIdAsync(userId) is { } user)
                {
                    await notifications.SendAsync(user, new NotificationMessage
                    {
                        Subject = S["Approval asked: {0}", request.Title],
                        Summary = S["{0} asks for your approval under {1}.", request.RequesterName ?? S["A workflow"].Value, request.PolicyTitle],
                        TextBody = S["Open the request to approve or reject it."],
                        Url = RequestUrl(request),
                        Type = ApprovalsConstants.ApprovalNotificationType,
                        ReferenceId = request.ItemId,
                    }, cancellationToken);
                }
            }

            return;
        }

        if (change.Kind is not (ApprovalChangeKind.Approved or ApprovalChangeKind.Rejected or ApprovalChangeKind.Canceled))
        {
            return;
        }

        // A decided request leaves no open notice behind.
        if (_serviceProvider.GetService<INotificationManager>() is { } manager)
        {
            await manager.DeleteAsync(new NotificationFilter
            {
                Type = ApprovalsConstants.ApprovalNotificationType,
                ReferenceId = request.ItemId,
            }, cancellationToken);
        }

        if (change.Kind != ApprovalChangeKind.Canceled && request.RequesterUserId is not null && await userManager.FindByIdAsync(request.RequesterUserId) is { } requester)
        {
            await notifications.SendAsync(requester, new NotificationMessage
            {
                Subject = change.Kind == ApprovalChangeKind.Approved ? S["Approved: {0}", request.Title] : S["Rejected: {0}", request.Title],
                Summary = change.Kind == ApprovalChangeKind.Approved
                    ? S["Your request was approved."]
                    : S["{0} rejected your request: {1}", change.Actor ?? string.Empty, change.Decision?.Note ?? string.Empty],
                Url = string.IsNullOrEmpty(request.SubjectUrl) ? RequestUrl(request) : request.SubjectUrl,
                Type = ApprovalsConstants.DecisionNotificationType,
                ReferenceId = request.ItemId,
            }, cancellationToken);
        }
    }
}
