using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Transactions.Workflows;

/// <summary>
/// Starts the transaction workflow events. The input is taken when the change happens, and the workflows run after the
/// request's work is saved, so a workflow task that runs a command on the same transaction never waits on the lock of the
/// command that raised the event.
/// </summary>
internal sealed class TransactionWorkflowEventHandler : TransactionEventHandlerBase, ITransactionPaymentHandler
{
    private readonly IWorkflowManager _workflowManager;

    public TransactionWorkflowEventHandler(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public override Task CreatedAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(TransactionCreatedEvent), transaction, TransactionWorkflowInput.ToDictionary(transaction));

    public override Task StatusChangedAsync(Transaction transaction, TransactionStatus previous, TransactionStatus current, CancellationToken cancellationToken = default)
        => current == TransactionStatus.Paid
            ? TriggerAsync(nameof(TransactionSettledEvent), transaction, TransactionWorkflowInput.ToDictionary(transaction))
            : Task.CompletedTask;

    public override Task CanceledAsync(Transaction transaction, string reason, CancellationToken cancellationToken = default)
    {
        var input = TransactionWorkflowInput.ToDictionary(transaction);
        input["Reason"] = reason;

        return TriggerAsync(nameof(TransactionCanceledEvent), transaction, input);
    }

    public override Task BecameOverdueAsync(Transaction transaction, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(TransactionOverdueEvent), transaction, TransactionWorkflowInput.ToDictionary(transaction));

    public override Task RefundedAsync(Transaction transaction, TransactionEvent refund, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(TransactionRefundedEvent), transaction, TransactionWorkflowInput.ToDictionary(transaction, refund));

    public override Task ReversedAsync(Transaction transaction, TransactionEvent reversal, TransactionEvent reversed, CancellationToken cancellationToken = default)
    {
        var input = TransactionWorkflowInput.ToDictionary(transaction, reversal);
        input["Reason"] = reversal?.Reason;
        input["ReversedEventId"] = reversed?.Id;
        input["ReversedEventType"] = reversed?.Type.ToString();

        return TriggerAsync(nameof(TransactionEntryReversedEvent), transaction, input);
    }

    public Task PaymentRecordedAsync(TransactionPaymentRecordedContext context, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(TransactionPaymentRecordedEvent), context.Transaction, TransactionWorkflowInput.ToDictionary(context.Transaction, context.Payment));

    private Task TriggerAsync(string eventName, Transaction transaction, Dictionary<string, object> input)
    {
        var correlationId = transaction.ItemId;

        // Outside a shell scope (a test, a host task) there is nothing to wait for, so the workflows start at once.
        if (ShellScope.Current is null)
        {
            return _workflowManager.TriggerEventAsync(eventName, input, correlationId);
        }

        DeferredWork.Add("transaction_workflows", scope => scope.ServiceProvider
            .GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(eventName, input, correlationId));

        return Task.CompletedTask;
    }
}
