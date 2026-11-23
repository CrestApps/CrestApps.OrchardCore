using CrestApps.OrchardCore.MailingLists.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.MailingLists.Workflows;

/// <summary>
/// Starts the mailing list member workflow events after the request commits.
/// </summary>
internal sealed class MailingListWorkflowObserver : MailingListMemberObserverBase
{
    public override Task SubscribedAsync(MailingListMemberContext context)
        => TriggerAsync(nameof(MailingListMemberSubscribedEvent), context);

    public override Task VerifiedAsync(MailingListMemberContext context)
        => TriggerAsync(nameof(MailingListMemberVerifiedEvent), context);

    public override Task UnsubscribedAsync(MailingListMemberContext context)
        => TriggerAsync(nameof(MailingListMemberUnsubscribedEvent), context);

    private static Task TriggerAsync(string name, MailingListMemberContext context)
    {
        var member = context.Member;
        var input = new Dictionary<string, object>
        {
            ["ListId"] = member.ListId,
            ["ListName"] = context.List?.Name,
            ["MemberId"] = member.ItemId,
            ["Email"] = member.Contact?.Email ?? member.NormalizedEmail,
            ["Name"] = member.Contact?.Name,
            ["Status"] = member.Status.ToString(),
            ["Source"] = context.Source,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, member.ItemId));

        return Task.CompletedTask;
    }
}
