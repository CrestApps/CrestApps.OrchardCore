using System.Globalization;
using CrestApps.OrchardCore.Payroll.Models;
using CrestApps.OrchardCore.Payroll.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Payroll.Workflows;

/// <summary>
/// Starts Payroll's workflow events once the change is committed (a deferred task of the shell scope), so a workflow
/// never sees a change that was rolled back. Amounts are in the input for the workflow's own use; nothing is sent.
/// </summary>
internal sealed class PayrollWorkflowObserver : IPayRunObserver, IPayStatementObserver, IPayProfileObserver
{
    /// <inheritdoc />
    public Task ChangedAsync(PayRunChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        var names = EventsFor(change.Kind);

        var run = change.Run;
        var input = new Dictionary<string, object>
        {
            ["PayRunId"] = run.ItemId,
            ["Number"] = run.Number,
            ["EmployerId"] = run.EmployerId,
            ["PayScheduleId"] = run.PayScheduleId,
            ["Type"] = run.Type.ToString(),
            ["PeriodStart"] = Day(run.PeriodStart),
            ["PeriodEnd"] = Day(run.PeriodEnd),
            ["PayDate"] = Day(run.PayDate),
            ["Status"] = run.Status.ToString(),
            ["Statements"] = run.Totals.Statements,
            ["Change"] = change.Kind.ToString(),
            ["Actor"] = change.Actor,
            ["Note"] = change.Note,
        };

        Trigger(names, input, run.ItemId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task IssuedAsync(PayStatement statement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        Trigger([nameof(PayStatementIssuedEvent)], StatementInput(statement, null, "Issued"), statement.ItemId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task VoidedAsync(PayStatement statement, PayStatement reversal, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(statement);

        Trigger([nameof(PayStatementVoidedEvent)], StatementInput(statement, reversal?.ItemId, "Voided"), statement.ItemId);

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task ChangedAsync(PayProfileChange change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);

        // A reveal of the tax id is a read, not a change; the audit trail records it.
        if (change.Kind == PayProfileChangeKind.TaxIdRevealed || change.Profile is null)
        {
            return Task.CompletedTask;
        }

        Trigger([nameof(PayProfileChangedEvent)], new Dictionary<string, object>
        {
            ["ProfileId"] = change.Profile.ItemId,
            ["StaffMemberId"] = change.Profile.StaffMemberId,
            ["EmployerId"] = change.Profile.EmployerId,
            ["Change"] = change.Kind.ToString(),
            ["Actor"] = change.Actor,
            ["Reason"] = change.Reason,
        }, change.Profile.ItemId);

        return Task.CompletedTask;
    }

    // The events a change of a run starts: processing starts Processed, and Paid or Failed when it is one of those.
    internal static string[] EventsFor(PayRunChangeKind kind)
        => kind switch
        {
            PayRunChangeKind.Opened => [nameof(PayRunOpenedEvent)],
            PayRunChangeKind.Calculated => [nameof(PayRunCalculatedEvent)],
            PayRunChangeKind.Submitted => [nameof(PayRunSubmittedEvent)],
            PayRunChangeKind.Approved => [nameof(PayRunApprovedEvent)],
            PayRunChangeKind.Rejected => [nameof(PayRunRejectedEvent)],
            PayRunChangeKind.Paid => [nameof(PayRunProcessedEvent), nameof(PayRunPaidEvent)],
            PayRunChangeKind.PartiallyPaid => [nameof(PayRunProcessedEvent)],
            PayRunChangeKind.Failed => [nameof(PayRunProcessedEvent), nameof(PayRunFailedEvent)],
            _ => Array.Empty<string>(),
        };

    private static Dictionary<string, object> StatementInput(PayStatement statement, string reversalId, string change)
        => new()
        {
            ["StatementId"] = statement.ItemId,
            ["Number"] = statement.Number,
            ["RunId"] = statement.RunId,
            ["StaffMemberId"] = statement.StaffMemberId,
            ["EmployerId"] = statement.EmployerId,
            ["PayDate"] = Day(statement.PayDate),
            ["Net"] = statement.Net,
            ["Currency"] = statement.Currency,
            ["Status"] = statement.Status.ToString(),
            ["ReversalId"] = reversalId,
            ["Change"] = change,
        };

    private static void Trigger(string[] names, Dictionary<string, object> input, string correlationId)
    {
        if (names.Length == 0)
        {
            return;
        }

        ShellScope.AddDeferredTask(async scope =>
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();

            foreach (var name in names)
            {
                await workflows.TriggerEventAsync(name, input, correlationId);
            }
        });
    }

    private static string Day(DateOnly day)
        => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
