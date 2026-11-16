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
        var name = change.Kind switch
        {
            ApprovalChangeKind.Requested => nameof(ApprovalRequestedEvent),
            ApprovalChangeKind.Approved => nameof(ApprovalApprovedEvent),
            ApprovalChangeKind.Rejected => nameof(ApprovalRejectedEvent),
            _ => null,
        };

        if (name is null)
        {
            return;
        }

        var request = change.Request;
        var approvers = new List<string>();

        // The approvers' user names, for Notify User Task in a reminder workflow.
        if (change.Kind == ApprovalChangeKind.Requested && _serviceProvider.GetService<UserManager<IUser>>() is { } userManager)
        {
            foreach (var userId in await _serviceProvider.GetRequiredService<IApprovalService>().GetApproverUserIdsAsync(request, cancellationToken))
            {
                if (await userManager.FindByIdAsync(userId) is { } user)
                {
                    approvers.Add(user.UserName);
                }
            }
        }

        var input = new Dictionary<string, object>
        {
            ["RequestId"] = request.ItemId,
            ["SubjectKind"] = request.SubjectKind,
            ["SubjectId"] = request.SubjectId,
            ["Title"] = request.Title,
            ["PolicyId"] = request.PolicyId,
            ["PolicyTitle"] = request.PolicyTitle,
            ["Status"] = request.Status.ToString(),
            ["Change"] = change.Kind.ToString(),
            ["RequesterName"] = request.RequesterName,
            ["ApproverUserNames"] = string.Join(",", approvers),
            ["Decider"] = change.Actor,
            ["Note"] = change.Decision?.Note,
            ["DueUtc"] = request.DueUtc,
            ["Stage"] = request.CurrentStage + 1,
            ["Amount"] = request.Amount,
            ["Currency"] = request.Currency,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, request.ItemId));
    }
}
