using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Banking.Workflows;

/// <summary>
/// Starts the Bank Account Changed event once the change is committed. A reveal is not a change and starts nothing;
/// the input never holds a number.
/// </summary>
internal sealed class BankingWorkflowObserver : IBankAccountObserver
{
    public Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        if (change.Has(BankAccountChangeKind.NumberRevealed))
        {
            return Task.CompletedTask;
        }

        var account = change.Account;
        var input = new Dictionary<string, object>
        {
            ["BankAccountId"] = account.ItemId,
            ["Name"] = account.Name,
            ["OwnerKind"] = account.Owner.Kind,
            ["OwnerId"] = account.Owner.Id,
            ["Status"] = account.Status.ToString(),
            ["PreviousStatus"] = change.PreviousStatus?.ToString(),
            ["Changes"] = change.Kinds.Select(kind => kind.ToString()).ToArray(),
            ["Actor"] = change.Actor,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(BankAccountChangedEvent.EventName, input, account.ItemId));

        return Task.CompletedTask;
    }
}
