using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Invoicing.Models;
using CrestApps.OrchardCore.Invoicing.Services;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Models;
using CrestApps.OrchardCore.WorkOrders.Services;
using Microsoft.Extensions.Logging;
using OrchardCore;

namespace CrestApps.OrchardCore.Invoicing.Services;

/// <summary>
/// Bills a completed work order as an invoice: labor as service lines and parts as item lines, billed to the work order's
/// account, customer or contact, with the work order as its source; the invoice is issued at once, which posts what is
/// owed. One invoice per work order: billing it again returns the first. Wins over the plain debt Transactions posts.
/// </summary>
public sealed class InvoicingWorkOrderBilling : IWorkOrderBilling
{
    private readonly IInvoiceManager _invoices;
    private readonly IInvoiceCommandService _commands;
    private readonly ILogger _logger;

    public InvoicingWorkOrderBilling(IInvoiceManager invoices, IInvoiceCommandService commands, ILogger<InvoicingWorkOrderBilling> logger)
    {
        _invoices = invoices;
        _commands = commands;
        _logger = logger;
    }

    /// <inheritdoc/>
    public int Priority
        => 10;

    /// <inheritdoc/>
    public async Task<WorkOrderBillingResult> InvoiceAsync(WorkOrderBillingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.WorkOrder);

        var workOrder = request.WorkOrder;
        var existing = await _invoices.PageAsync(1, 1, new InvoiceQuery { SourceType = WorkOrdersConstants.ReferenceType, SourceId = workOrder.ItemId }, cancellationToken);

        if (existing.Entries.FirstOrDefault() is { } billed)
        {
            return Result(billed);
        }

        var invoice = await _invoices.NewAsync(cancellationToken: cancellationToken);

        invoice.Currency = request.Currency;
        invoice.PaymentTermId = request.PaymentTermId;
        invoice.SourceType = WorkOrdersConstants.ReferenceType;
        invoice.SourceId = workOrder.ItemId;
        invoice.Reference = workOrder.Number;
        invoice.BillTo = new DocumentParty
        {
            Party = request.Payer,
            Contact = string.IsNullOrWhiteSpace(workOrder.Contact?.Name) && string.IsNullOrWhiteSpace(workOrder.Contact?.Email)
                ? null
                : ContactInfo.From(workOrder.Contact.Name, workOrder.Contact.Email, workOrder.Contact.Phone),
        };

        foreach (var line in request.Lines.Where(line => line.Quantity > 0m))
        {
            invoice.Lines.Add(new DocumentLine
            {
                Id = IdGenerator.GenerateId(),
                Kind = line.Kind == WorkOrderLineKind.Product ? DocumentLineKind.Item : DocumentLineKind.Service,
                Name = line.Description,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                ProductId = line.ProductId,
                TaxCategoryCode = line.TaxCategoryCode ?? request.TaxCategoryCode,
                SourceType = WorkOrdersConstants.ReferenceType,
                SourceId = workOrder.ItemId,
            });
        }

        var validation = await _invoices.ValidateAsync(invoice, cancellationToken);

        if (!validation.Succeeded)
        {
            _logger.LogWarning("Work order '{WorkOrderId}' could not be invoiced: {Errors}", workOrder.ItemId, string.Join(" ", validation.Errors.Select(error => error.ErrorMessage)));

            return null;
        }

        await _invoices.CreateAsync(invoice, cancellationToken);

        var issued = await _commands.IssueAsync(invoice.ItemId, cancellationToken);

        if (!issued.Succeeded)
        {
            // Kept as a draft for someone to finish: nothing is owed until it is issued.
            _logger.LogWarning("The invoice of work order '{WorkOrderId}' was kept as a draft: {Errors}", workOrder.ItemId, string.Join(" ", issued.Errors));

            return new WorkOrderBillingResult { InvoiceId = invoice.ItemId };
        }

        return Result(issued.Invoice);
    }

    /// <inheritdoc/>
    public async Task<string> InvoiceAsync(WorkOrder workOrder, decimal amount, string currency, CancellationToken cancellationToken = default)
    {
        var result = await InvoiceAsync(new WorkOrderBillingRequest
        {
            WorkOrder = workOrder,
            Amount = amount,
            Currency = currency,
            Lines = [new WorkOrderBillingLine { Kind = WorkOrderLineKind.Service, Description = workOrder.Summary ?? workOrder.Number, Quantity = 1m, UnitPrice = amount }],
        }, cancellationToken);

        return result?.TransactionId;
    }

    private static WorkOrderBillingResult Result(Invoice invoice)
        => new()
        {
            TransactionId = invoice.TransactionId,
            InvoiceId = invoice.ItemId,
            InvoiceNumber = invoice.Number,
        };
}
