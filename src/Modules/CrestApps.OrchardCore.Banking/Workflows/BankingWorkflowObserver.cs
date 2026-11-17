using CrestApps.OrchardCore.Banking.Models;
using CrestApps.OrchardCore.Banking.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Banking.Workflows;

/// <summary>
/// Starts the Bank Account Changed event once the change is committed, and Bank Account Status Changed when the status
/// moved. A reveal is not a change and starts nothing; the input never holds a number.
/// </summary>
internal sealed class BankingWorkflowObserver : IBankAccountObserver
{
    public Task OnChangedAsync(BankAccountChange change, CancellationToken cancellationToken = default)
    {
        var names = EventsFor(change);

        if (names.Length == 0)
        {
            return Task.CompletedTask;
        }

        var input = Input(change);
        var accountId = change.Account.ItemId;

        ShellScope.AddDeferredTask(async scope =>
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();

            foreach (var name in names)
            {
                await workflows.TriggerEventAsync(name, input, accountId);
            }
        });

        return Task.CompletedTask;
    }

    // A changed number takes a verified account back to unverified: that is a status change too.
    internal static string[] EventsFor(BankAccountChange change)
    {
        if (change.Has(BankAccountChangeKind.NumberRevealed))
        {
            return [];
        }

        var statusChanged = !change.Has(BankAccountChangeKind.Created)
            && !change.Has(BankAccountChangeKind.Deleted)
            && (change.Has(BankAccountChangeKind.StatusChanged) || (change.PreviousStatus is { } previous && previous != change.Account.Status));

        return statusChanged
            ? [BankAccountChangedEvent.EventName, nameof(BankAccountStatusChangedEvent)]
            : [BankAccountChangedEvent.EventName];
    }

    internal static Dictionary<string, object> Input(BankAccountChange change)
    {
        var account = change.Account;

        return new Dictionary<string, object>
        {
            ["BankAccountId"] = account.ItemId,
            ["Name"] = account.Name,
            ["OwnerKind"] = account.Owner.Kind,
            ["OwnerId"] = account.Owner.Id,
            ["Status"] = account.Status.ToString(),
            ["PreviousStatus"] = change.PreviousStatus?.ToString(),
            ["Reason"] = change.Reason,
            ["Changes"] = change.Kinds.Select(kind => kind.ToString()).ToArray(),
            ["Actor"] = change.Actor,
        };
    }
}
