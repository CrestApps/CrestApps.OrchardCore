using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// Takes the parts used on a work order off a stock location.
/// </summary>
public interface IWorkOrderPartsService
{
    /// <summary>
    /// Records parts used, taking them off the location.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="line">The line, with its stock item and location.</param>
    /// <param name="quantity">How many.</param>
    /// <param name="actor">Who.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>Why it was refused, or <see langword="null"/>.</returns>
    Task<string> UseAsync(WorkOrder workOrder, WorkOrderLine line, decimal quantity, string actor, CancellationToken cancellationToken = default);
}

/// <summary>
/// Invoices a completed work order. When several are registered, the one with the highest <see cref="Priority"/> bills:
/// Invoicing's (a numbered invoice with the lines) wins over the plain debt Transactions posts.
/// </summary>
public interface IWorkOrderBilling
{
    /// <summary>
    /// Gets the priority of this billing over the others registered.
    /// </summary>
    int Priority
        => 0;

    /// <summary>
    /// Bills a completed work order with its lines, payer, term and tax category. Billing that only posts an amount
    /// implements <see cref="InvoiceAsync(WorkOrder, decimal, string, CancellationToken)"/> and gets this from it.
    /// </summary>
    /// <param name="request">What to bill.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>What it created.</returns>
    async Task<WorkOrderBillingResult> InvoiceAsync(WorkOrderBillingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return new WorkOrderBillingResult { TransactionId = await InvoiceAsync(request.WorkOrder, request.Amount, request.Currency, cancellationToken) };
    }

    /// <summary>
    /// Creates the invoice.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="amount">The amount.</param>
    /// <param name="currency">The currency.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The invoice's transaction id.</returns>
    Task<string> InvoiceAsync(WorkOrder workOrder, decimal amount, string currency, CancellationToken cancellationToken = default);
}

/// <summary>
/// Hears what happens to work orders, after the work is saved.
/// </summary>
public interface IWorkOrderObserver
{
    /// <summary>
    /// Called for each change.
    /// </summary>
    /// <param name="workOrder">The work order.</param>
    /// <param name="eventName">The event, one of <see cref="WorkOrdersConstants.Events"/>.</param>
    /// <param name="detail">The detail, such as the old status.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task.</returns>
    Task OnEventAsync(WorkOrder workOrder, string eventName, IReadOnlyDictionary<string, object> detail, CancellationToken cancellationToken = default);
}
