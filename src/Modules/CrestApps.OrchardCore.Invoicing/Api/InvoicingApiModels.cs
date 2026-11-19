using CrestApps.OrchardCore.Commerce.Api;
using CrestApps.OrchardCore.Invoicing.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Invoicing.Api;

/// <summary>
/// Maps the invoicing API's endpoints when the Invoicing API feature is on.
/// </summary>
[Feature(InvoicingConstants.Features.Api)]
public sealed class InvoicingApiStartup : StartupBase
{
    /// <inheritdoc/>
    public override void Configure(IApplicationBuilder app, IEndpointRouteBuilder routes, IServiceProvider serviceProvider)
        => routes.MapInvoicingApi();
}

/// <summary>
/// An invoice in a list.
/// </summary>
public sealed record ApiInvoiceSummary(
    string Id,
    string Number,
    string Kind,
    string Status,
    string Settlement,
    string CustomerName,
    string CustomerParty,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    ApiMoney Total,
    ApiMoney AmountDue);

/// <summary>
/// An invoice with its lines and amounts.
/// </summary>
public sealed record ApiInvoice(
    string Id,
    string Number,
    string Kind,
    string Status,
    string Settlement,
    string Currency,
    ApiInvoiceParty BillTo,
    DateOnly? IssueDate,
    DateOnly? DueDate,
    string PaymentTerm,
    string Reference,
    string PurchaseOrderNumber,
    IReadOnlyList<ApiInvoiceLine> Lines,
    IReadOnlyList<ApiInvoiceTax> Taxes,
    ApiMoney Subtotal,
    ApiMoney Tax,
    ApiMoney Total,
    ApiMoney AmountPaid,
    ApiMoney AmountCredited,
    ApiMoney AmountDue,
    string Notes,
    string Terms,
    string InternalNote,
    string RelatedInvoiceId,
    string SourceType,
    string SourceId,
    DateTime? IssuedUtc,
    DateTime? SentUtc);

/// <summary>
/// Who an invoice is billed to.
/// </summary>
public sealed record ApiInvoiceParty(string Party, string Name, string Email);

/// <summary>
/// A line of an invoice.
/// </summary>
public sealed record ApiInvoiceLine(string Id, string Kind, string Sku, string Name, string Description, decimal Quantity, string Unit, decimal UnitPrice, ApiMoney Net, ApiMoney Tax, ApiMoney Total);

/// <summary>
/// A row of an invoice's tax breakdown.
/// </summary>
public sealed record ApiInvoiceTax(string Name, decimal Rate, ApiMoney TaxableAmount, ApiMoney TaxAmount);

/// <summary>
/// An invoice to draft.
/// </summary>
public sealed class ApiInvoiceDraft
{
    public ApiInvoiceDraftParty BillTo { get; set; }

    public string Currency { get; set; }

    public bool? PricesIncludeTax { get; set; }

    public bool? AllowPartialPayment { get; set; }

    public bool AutoCharge { get; set; }

    public DateOnly? IssueDate { get; set; }

    public DateOnly? DueDate { get; set; }

    public string PaymentTermId { get; set; }

    public string Reference { get; set; }

    public string PurchaseOrderNumber { get; set; }

    public string Notes { get; set; }

    public string Terms { get; set; }

    public string InternalNote { get; set; }

    /// <summary>
    /// Gets or sets what made the invoice in the calling system, such as <c>crm-deal</c>, with <see cref="SourceId"/>.
    /// </summary>
    public string SourceType { get; set; }

    public string SourceId { get; set; }

    public IList<ApiInvoiceDraftLine> Lines { get; set; } = [];

    /// <summary>
    /// Gets or sets whether the draft is issued at once.
    /// </summary>
    public bool Issue { get; set; }
}

/// <summary>
/// Who a drafted invoice is billed to: a party (<c>User:42</c>, <c>Account:7</c>, <c>Customer:9</c>), or a name and e-mail.
/// </summary>
public sealed class ApiInvoiceDraftParty
{
    public string Party { get; set; }

    public string Name { get; set; }

    public string Email { get; set; }
}

/// <summary>
/// A line of an invoice to draft.
/// </summary>
public sealed class ApiInvoiceDraftLine
{
    public string Kind { get; set; }

    public string Sku { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public decimal? Quantity { get; set; }

    public string Unit { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal? DiscountPercent { get; set; }

    public string TaxCategoryCode { get; set; }
}

/// <summary>
/// Where to send an invoice, when not to its customer's address.
/// </summary>
public sealed class ApiInvoiceSendRequest
{
    public string To { get; set; }
}

/// <summary>
/// Why an invoice is voided.
/// </summary>
public sealed class ApiInvoiceReasonRequest
{
    public string Reason { get; set; }
}

/// <summary>
/// A payment taken outside the site.
/// </summary>
public sealed class ApiInvoicePaymentRequest
{
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets how it was paid, such as <c>check</c>, <c>bank-transfer</c> or <c>cash</c>.
    /// </summary>
    public string Method { get; set; }

    public string Reference { get; set; }

    public string Note { get; set; }
}

internal static class InvoicingApiMapper
{
    public static ApiInvoiceSummary ToSummary(Invoice invoice)
        => new(
            invoice.ItemId,
            invoice.Number,
            ToCamel(invoice.Kind.ToString()),
            ToCamel(invoice.Status.ToString()),
            ToCamel(invoice.Settlement.ToString()),
            invoice.BillTo?.Contact?.Name,
            invoice.BillTo?.Party.IsEmpty == false ? invoice.BillTo.Party.ToString() : null,
            invoice.IssueDate,
            invoice.DueDate,
            ApiMoney.From(invoice.Totals?.Total ?? 0m, invoice.Currency),
            ApiMoney.From(invoice.AmountDue, invoice.Currency));

    public static ApiInvoice ToInvoice(Invoice invoice, bool forCustomer = false)
        => new(
            invoice.ItemId,
            invoice.Number,
            ToCamel(invoice.Kind.ToString()),
            ToCamel(invoice.Status.ToString()),
            ToCamel(invoice.Settlement.ToString()),
            invoice.Currency,
            new ApiInvoiceParty(invoice.BillTo?.Party.IsEmpty == false ? invoice.BillTo.Party.ToString() : null, invoice.BillTo?.Contact?.Name, invoice.BillTo?.Contact?.Email),
            invoice.IssueDate,
            invoice.DueDate,
            invoice.PaymentTermName,
            invoice.Reference,
            invoice.PurchaseOrderNumber,
            [.. invoice.Lines.Select(line => new ApiInvoiceLine(
                line.Id,
                ToCamel(line.Kind.ToString()),
                line.Sku,
                line.Name,
                line.Description,
                line.Quantity,
                line.Unit,
                line.UnitPrice,
                ApiMoney.From(line.Net, invoice.Currency),
                ApiMoney.From(line.TaxAmount, invoice.Currency),
                ApiMoney.From(line.Total, invoice.Currency)))],
            [.. (invoice.Taxes ?? []).Select(tax => new ApiInvoiceTax(tax.Name, tax.Rate, ApiMoney.From(tax.TaxableAmount, invoice.Currency), ApiMoney.From(tax.TaxAmount, invoice.Currency)))],
            ApiMoney.From(invoice.Totals?.Subtotal ?? 0m, invoice.Currency),
            ApiMoney.From(invoice.Totals?.TaxTotal ?? 0m, invoice.Currency),
            ApiMoney.From(invoice.Totals?.Total ?? 0m, invoice.Currency),
            ApiMoney.From(invoice.AmountPaid, invoice.Currency),
            ApiMoney.From(invoice.AmountCredited, invoice.Currency),
            ApiMoney.From(invoice.AmountDue, invoice.Currency),
            invoice.Notes,
            invoice.Terms,

            // Staff's own note is never given to the customer.
            forCustomer ? null : invoice.InternalNote,
            invoice.RelatedInvoiceId,
            forCustomer ? null : invoice.SourceType,
            forCustomer ? null : invoice.SourceId,
            invoice.IssuedUtc,
            invoice.SentUtc);

    private static string ToCamel(string value)
        => string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];
}
