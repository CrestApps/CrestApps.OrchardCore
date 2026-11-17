using System.Globalization;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Banking.Disbursements.Workflows;

/// <summary>
/// Starts the Disbursements workflow events after the change is saved. Each change starts exactly one event: a
/// cancelled or reversed disbursement starts Disbursement Canceled or Disbursement Reversed, not Disbursement Failed.
/// </summary>
internal sealed class DisbursementsWorkflowObserver : IDisbursementObserver, IDisbursementBatchObserver
{
    public Task OnChangedAsync(DisbursementChange change, CancellationToken cancellationToken = default)
    {
        var names = EventsFor(change.Kind);

        if (names.Length > 0)
        {
            Trigger(names, Input(change), change.Disbursement.ItemId);
        }

        return Task.CompletedTask;
    }

    public Task OnChangedAsync(DisbursementBatchChange change, CancellationToken cancellationToken = default)
    {
        var names = EventsFor(change);

        if (names.Length > 0)
        {
            Trigger(names, BatchInput(change), change.Batch.ItemId);
        }

        return Task.CompletedTask;
    }

    public Task OnReturnsAppliedAsync(DisbursementImportResult result, string actor, CancellationToken cancellationToken = default)
    {
        Trigger([nameof(DisbursementReturnsAppliedEvent)], ReturnsInput(result, actor), null);

        return Task.CompletedTask;
    }

    internal static string[] EventsFor(DisbursementChangeKind kind)
        => kind switch
        {
            DisbursementChangeKind.Created => [nameof(DisbursementCreatedEvent)],
            DisbursementChangeKind.Batched => [nameof(DisbursementBatchedEvent)],
            DisbursementChangeKind.Unbatched => [nameof(DisbursementUnbatchedEvent)],
            DisbursementChangeKind.Submitted => [nameof(DisbursementSubmittedEvent)],
            DisbursementChangeKind.Settled => [nameof(DisbursementSettledEvent)],
            DisbursementChangeKind.Returned => [nameof(DisbursementReturnedEvent)],
            DisbursementChangeKind.Failed => [nameof(DisbursementFailedEvent)],
            DisbursementChangeKind.Canceled => [nameof(DisbursementCanceledEvent)],
            DisbursementChangeKind.Reversed => [nameof(DisbursementReversedEvent)],
            _ => [],
        };

    // A release that the provider paid in full settles the batch at once: it starts Released and Settled.
    internal static string[] EventsFor(DisbursementBatchChange change)
        => change.Kind switch
        {
            DisbursementBatchChangeKind.Created => [nameof(DisbursementBatchCreatedEvent)],
            DisbursementBatchChangeKind.Released when change.Batch.Status == DisbursementBatchStatus.Settled => [nameof(DisbursementBatchReleasedEvent), nameof(DisbursementBatchSettledEvent)],
            DisbursementBatchChangeKind.Released => [nameof(DisbursementBatchReleasedEvent)],
            DisbursementBatchChangeKind.Reopened => [nameof(DisbursementBatchReopenedEvent)],
            DisbursementBatchChangeKind.Canceled => [nameof(DisbursementBatchCanceledEvent)],
            DisbursementBatchChangeKind.Settled => [nameof(DisbursementBatchSettledEvent)],
            _ => [],
        };

    // Never an account number: the input names the payee and the record paid.
    internal static Dictionary<string, object> Input(DisbursementChange change)
    {
        var disbursement = change.Disbursement;

        return new Dictionary<string, object>
        {
            ["DisbursementId"] = disbursement.ItemId,
            ["PayeeName"] = disbursement.PayeeName,
            ["Amount"] = disbursement.Amount,
            ["Currency"] = disbursement.Currency,
            ["Method"] = disbursement.Method,
            ["Status"] = disbursement.Status.ToString(),
            ["Change"] = change.Kind.ToString(),
            ["SourceKind"] = disbursement.SourceKind,
            ["SourceId"] = disbursement.SourceId,
            ["BatchId"] = disbursement.BatchId,
            ["TraceNumber"] = disbursement.TraceNumber,
            ["FailureCode"] = disbursement.FailureCode,
            ["FailureReason"] = disbursement.FailureReason,
            ["IsPrenote"] = disbursement.IsPrenote,
            ["EffectiveDate"] = disbursement.EffectiveDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Actor"] = change.Actor,
        };
    }

    // The batch's file and its account numbers are never in the input.
    internal static Dictionary<string, object> BatchInput(DisbursementBatchChange change)
    {
        var batch = change.Batch;

        return new Dictionary<string, object>
        {
            ["BatchId"] = batch.ItemId,
            ["Provider"] = batch.Provider,
            ["SourceBankAccountId"] = batch.SourceBankAccountId,
            ["SourceBankAccountName"] = batch.SourceBankAccountName,
            ["EffectiveDate"] = batch.EffectiveDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["Count"] = batch.Count,
            ["TotalAmount"] = batch.TotalAmount,
            ["Currency"] = batch.Currency,
            ["Status"] = batch.Status.ToString(),
            ["PreviousStatus"] = change.PreviousStatus?.ToString(),
            ["SourceKind"] = batch.SourceKind,
            ["AccessScope"] = batch.AccessScope,
            ["FileName"] = batch.FileName,
            ["ProviderReference"] = batch.ProviderReference,
            ["Change"] = change.Kind.ToString(),
            ["Reason"] = change.Reason,
            ["Settled"] = change.Settled,
            ["Actor"] = change.Actor,
        };
    }

    internal static Dictionary<string, object> ReturnsInput(DisbursementImportResult result, string actor)
        => new()
        {
            ["Returned"] = result.Returned,
            ["Problems"] = result.Problems.Count,
            ["ProblemMessages"] = result.Problems.ToArray(),
            ["Actor"] = actor,
        };

    private static void Trigger(string[] names, Dictionary<string, object> input, string correlationId)
        => ShellScope.AddDeferredTask(async scope =>
        {
            var workflows = scope.ServiceProvider.GetRequiredService<IWorkflowManager>();

            foreach (var name in names)
            {
                await workflows.TriggerEventAsync(name, input, correlationId);
            }
        });
}
