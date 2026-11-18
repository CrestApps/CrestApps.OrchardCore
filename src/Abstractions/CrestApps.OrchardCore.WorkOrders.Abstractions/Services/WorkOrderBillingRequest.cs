using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.WorkOrders.Models;

namespace CrestApps.OrchardCore.WorkOrders.Services;

/// <summary>
/// What a completed work order bills: who owes it, its lines, and the term and tax category to bill them with.
/// </summary>
public sealed class WorkOrderBillingRequest
{
    /// <summary>
    /// Gets or sets the work order.
    /// </summary>
    public WorkOrder WorkOrder { get; set; }

    /// <summary>
    /// Gets or sets the amount billed, before tax: the lines' total, or the agreed or fixed price.
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the lines billed. A work order billed at an agreed or fixed price has one line for it.
    /// </summary>
    public IList<WorkOrderBillingLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets who owes it: the work order's account, else its customer user, else its contact.
    /// </summary>
    public PartyRef Payer { get; set; }

    /// <summary>
    /// Gets or sets the payment term the invoice is due by, or <see langword="null"/> for the payer's or the default.
    /// </summary>
    public string PaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the tax category of the lines that name none, or <see langword="null"/>.
    /// </summary>
    public string TaxCategoryCode { get; set; }
}

/// <summary>
/// A line a completed work order bills.
/// </summary>
public sealed class WorkOrderBillingLine
{
    /// <summary>
    /// Gets or sets whether it is labor (a service) or a part (a product).
    /// </summary>
    public WorkOrderLineKind Kind { get; set; }

    /// <summary>
    /// Gets or sets what was done or used.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets how much.
    /// </summary>
    public decimal Quantity { get; set; }

    /// <summary>
    /// Gets or sets the price of one.
    /// </summary>
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets or sets the product, for a part.
    /// </summary>
    public string ProductId { get; set; }

    /// <summary>
    /// Gets or sets the tax category, or <see langword="null"/> for the request's.
    /// </summary>
    public string TaxCategoryCode { get; set; }
}

/// <summary>
/// What billing a work order created.
/// </summary>
public sealed class WorkOrderBillingResult
{
    /// <summary>
    /// Gets or sets the transaction of what is owed, or <see langword="null"/> when nothing was posted yet (an invoice
    /// left as a draft).
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the invoice document, when the billing made one.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the invoice's number, when it was issued.
    /// </summary>
    public string InvoiceNumber { get; set; }
}
