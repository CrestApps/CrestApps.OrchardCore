using System.Globalization;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts the Checks workflow events once the change is committed.
/// </summary>
internal sealed class ChecksWorkflowObserver : ICheckObserver
{
    public Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default)
    {
        var name = change.Kind switch
        {
            CheckChangeKind.Issued => nameof(CheckIssuedEvent),
            CheckChangeKind.Printed or CheckChangeKind.Reprinted => nameof(CheckPrintedEvent),
            CheckChangeKind.Cleared => nameof(CheckClearedEvent),
            CheckChangeKind.Voided or CheckChangeKind.StopPayment or CheckChangeKind.Reissued => nameof(CheckVoidedEvent),
            _ => null,
        };

        if (name is null)
        {
            return Task.CompletedTask;
        }

        var check = change.Check;
        var input = new Dictionary<string, object>
        {
            ["CheckId"] = check.ItemId,
            ["Number"] = check.Number,
            ["BankAccountId"] = check.BankAccountId,
            ["PayeeName"] = check.PayeeName,
            ["Amount"] = check.Amount,
            ["Currency"] = check.Currency,
            ["CheckDate"] = check.CheckDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["SourceKind"] = check.SourceKind,
            ["SourceId"] = check.SourceId,
            ["Status"] = check.Status.ToString(),
            ["Change"] = change.Kind.ToString(),
            ["ReplacedById"] = change.ReplacedBy?.ItemId,
            ["Reason"] = change.Reason,
            ["Actor"] = change.Actor,
        };

        ShellScope.AddDeferredTask(scope => scope.ServiceProvider.GetRequiredService<IWorkflowManager>().TriggerEventAsync(name, input, check.ItemId));

        return Task.CompletedTask;
    }
}
