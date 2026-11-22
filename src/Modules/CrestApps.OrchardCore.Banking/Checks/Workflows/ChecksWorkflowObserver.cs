using System.Globalization;
using CrestApps.OrchardCore.Banking.Checks.Models;
using CrestApps.OrchardCore.Banking.Checks.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Banking.Checks.Workflows;

/// <summary>
/// Starts the Checks workflow events once the change is committed. Each change starts exactly one event: a stopped or
/// reissued check starts Check Stop Payment or Check Reissued, not Check Voided.
/// </summary>
internal sealed class ChecksWorkflowObserver : ICheckObserver
{
    public Task OnChangedAsync(CheckChange change, CancellationToken cancellationToken = default)
    {
        var names = EventsFor(change.Kind);

        if (names.Length == 0)
        {
            return Task.CompletedTask;
        }

        var input = Input(change);
        var checkId = change.Check.ItemId;

        ShellScope.AddDeferredTask(async scope =>
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();

            foreach (var name in names)
            {
                await workflows.TriggerEventAsync(name, input, checkId);
            }
        });

        return Task.CompletedTask;
    }

    internal static string[] EventsFor(CheckChangeKind kind)
        => kind switch
        {
            CheckChangeKind.Issued => [nameof(CheckIssuedEvent)],
            CheckChangeKind.Printed or CheckChangeKind.Reprinted => [nameof(CheckPrintedEvent)],
            CheckChangeKind.Cleared => [nameof(CheckClearedEvent)],
            CheckChangeKind.Voided or CheckChangeKind.Spoiled => [nameof(CheckVoidedEvent)],
            CheckChangeKind.Uncleared => [nameof(CheckUnclearedEvent)],
            CheckChangeKind.StopPayment => [nameof(CheckStopPaymentEvent)],
            CheckChangeKind.Reissued => [nameof(CheckReissuedEvent)],
            CheckChangeKind.Stale => [nameof(CheckStaleEvent)],
            _ => [],
        };

    internal static Dictionary<string, object> Input(CheckChange change)
    {
        var check = change.Check;

        return new Dictionary<string, object>
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
            ["VoidedOn"] = change.VoidedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["ClearedOn"] = change.ClearedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }
}
