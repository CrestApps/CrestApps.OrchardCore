using CrestApps.OrchardCore.Approvals.Models;
using CrestApps.OrchardCore.Approvals.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Users;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Approvals.Workflows;

/// <summary>
/// Starts the Approvals workflow events after the change is saved. The correlation id is the request id, so a
/// workflow that waits on a timer finds its request again.
/// </summary>
internal sealed class ApprovalsWorkflowObserver : IApprovalObserver
{
    private readonly IServiceProvider _serviceProvider;

    public ApprovalsWorkflowObserver(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task OnChangedAsync(ApprovalChange change, CancellationToken cancellationToken = default)
    {
        var name = EventFor(change.Kind);

        if (name is null)
        {
            return;
        }

        var request = change.Request;
        var approvers = new List<string>();

        // The approvers' user names, for Notify User Task in a reminder workflow; after a stage, the next stage's.
        if (change.Kind is (ApprovalChangeKind.Requested or ApprovalChangeKind.StageApproved) && _serviceProvider.GetService<UserManager<IUser>>() is { } userManager)
        {
            foreach (var userId in await _serviceProvider.GetRequiredService<IApprovalService>().GetApproverUserIdsAsync(request, cancellationToken))
            {
                if (await userManager.FindByIdAsync(userId) is { } user)
                {
                    approvers.Add(user.UserName);
                }
            }
        }

        var input = Input(change, approvers);

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, request.ItemId));
    }

    // The event a change starts, or null for none.
    internal static string EventFor(ApprovalChangeKind kind)
        => kind switch
        {
            ApprovalChangeKind.Requested => nameof(ApprovalRequestedEvent),
            ApprovalChangeKind.PartlyApproved => nameof(ApprovalPartlyApprovedEvent),
            ApprovalChangeKind.StageApproved => nameof(ApprovalStageApprovedEvent),
            ApprovalChangeKind.Approved => nameof(ApprovalApprovedEvent),
            ApprovalChangeKind.Rejected => nameof(ApprovalRejectedEvent),
            ApprovalChangeKind.Canceled => nameof(ApprovalCanceledEvent),
            _ => null,
        };

    internal static Dictionary<string, object> Input(ApprovalChange change, IReadOnlyCollection<string> approvers)
        => new()
        {
            ["RequestId"] = change.Request.ItemId,
            ["SubjectKind"] = change.Request.SubjectKind,
            ["SubjectId"] = change.Request.SubjectId,
            ["Title"] = change.Request.Title,
            ["PolicyId"] = change.Request.PolicyId,
            ["PolicyTitle"] = change.Request.PolicyTitle,
            ["Status"] = change.Request.Status.ToString(),
            ["Change"] = change.Kind.ToString(),
            ["RequesterName"] = change.Request.RequesterName,
            ["ApproverUserNames"] = string.Join(",", approvers ?? []),
            ["Decider"] = change.Actor,
            ["Note"] = change.Decision?.Note ?? (change.Kind == ApprovalChangeKind.Canceled ? change.Request.CancelReason : null),
            ["DueUtc"] = change.Request.DueUtc,
            ["Stage"] = change.Request.CurrentStage + 1,
            ["Amount"] = change.Request.Amount,
            ["Currency"] = change.Request.Currency,
        };
}
