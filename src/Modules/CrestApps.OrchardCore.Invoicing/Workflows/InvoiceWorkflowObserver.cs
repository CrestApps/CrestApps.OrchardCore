using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Workflows.Services;

namespace CrestApps.OrchardCore.Invoicing.Workflows;

/// <summary>
/// Starts the invoice workflow events. The input is taken when the change happens, and the workflows run after the
/// request's work is saved, so a workflow task that runs a command on the same invoice never waits on the lock of the
/// command that raised the event.
/// </summary>
internal sealed class InvoiceWorkflowObserver : IInvoiceObserver
{
    public const string WorkName = "invoicing_workflows";

    private readonly IWorkflowManager _workflowManager;

    public InvoiceWorkflowObserver(IWorkflowManager workflowManager)
    {
        _workflowManager = workflowManager;
    }

    public Task CreatedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoiceCreatedEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task IssuedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoiceIssuedEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task SentAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoiceSentEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task ViewedAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoiceViewedEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task PaymentReceivedAsync(Invoice invoice, decimal amount, CancellationToken cancellationToken = default)
    {
        var input = InvoiceWorkflowInput.ToDictionary(invoice);
        input["Amount"] = amount;

        return TriggerAsync(nameof(InvoicePaymentReceivedEvent), invoice, input);
    }

    public Task PaidAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoicePaidEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task OverdueAsync(Invoice invoice, CancellationToken cancellationToken = default)
        => TriggerAsync(nameof(InvoiceOverdueEvent), invoice, InvoiceWorkflowInput.ToDictionary(invoice));

    public Task VoidedAsync(Invoice invoice, CancellationToken cancellationToken = default)
    {
        var input = InvoiceWorkflowInput.ToDictionary(invoice);
        input["Reason"] = invoice.VoidReason;

        return TriggerAsync(nameof(InvoiceVoidedEvent), invoice, input);
    }

    public Task CreditNoteIssuedAsync(CreditNote creditNote, Invoice invoice, CancellationToken cancellationToken = default)
    {
        var input = InvoiceWorkflowInput.ToDictionary(invoice);
        input["CreditNoteId"] = creditNote.ItemId;
        input["CreditNoteNumber"] = creditNote.Number;
        input["CreditNoteTotal"] = creditNote.Totals?.Total ?? 0m;
        input["Disposition"] = creditNote.Disposition.ToString();
        input["Reason"] = creditNote.Reason;

        return TriggerAsync(nameof(CreditNoteIssuedEvent), invoice, input);
    }

    private Task TriggerAsync(string eventName, Invoice invoice, Dictionary<string, object> input)
    {
        var correlationId = invoice.ItemId;

        // Outside a shell scope (a test, a host task) there is nothing to wait for, so the workflows start at once.
        if (ShellScope.Current is null)
        {
            return _workflowManager.TriggerEventAsync(eventName, input, correlationId);
        }

        DeferredWork.Add(WorkName, scope => scope.ServiceProvider
            .GetRequiredService<IWorkflowManager>()
            .TriggerEventAsync(eventName, input, correlationId));

        return Task.CompletedTask;
    }
}
