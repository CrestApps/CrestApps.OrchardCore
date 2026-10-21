using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionInvoiceBuilder"/>. An invoice uses the site's receipt branding and layout, and
/// shows the whole transaction: the receipts for the payments made against it are separate documents.
/// </summary>
public sealed class TransactionInvoiceBuilder : ITransactionInvoiceBuilder
{
    private readonly IReceiptService _receiptService;
    private readonly ICustomerContactResolver _contactResolver;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceBuilder"/> class.
    /// </summary>
    /// <param name="receiptService">The receipt service that applies the site's branding.</param>
    /// <param name="contactResolver">The resolver for whom the invoice is addressed to.</param>
    /// <param name="localClock">The clock that turns the stored UTC time into the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionInvoiceBuilder(
        IReceiptService receiptService,
        ICustomerContactResolver contactResolver,
        ILocalClock localClock,
        IStringLocalizer<TransactionInvoiceBuilder> stringLocalizer)
    {
        _receiptService = receiptService;
        _contactResolver = contactResolver;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<ReceiptDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var currency = transaction.Currency;
        var contact = await _contactResolver.ResolveAsync(transaction.GetOwner(), transaction.GetGuestContact(), cancellationToken);

        var paid = transaction.OutstandingAmount <= 0m;
        var notes = new List<string>();

        if (transaction.DueUtc.HasValue)
        {
            notes.Add(S["Due on {0:d}.", transaction.DueUtc.Value].Value);
        }

        if (!paid && transaction.AmountPaid > 0m)
        {
            notes.Add(S["{0} has been received; {1} is still due.", Format(transaction.AmountPaid, currency), Format(transaction.OutstandingAmount, currency)].Value);
        }

        var lineItems = new List<ReceiptLineItem>
        {
            new()
            {
                Description = string.IsNullOrEmpty(transaction.Title) ? S["Payment"].Value : transaction.Title,
                Quantity = 1,
                UnitAmount = transaction.TotalAmount - transaction.TaxAmount,
                Amount = transaction.TotalAmount - transaction.TaxAmount,
            },
        };

        // Each adjustment (a discount, waiver, credit or write-off) is a line that takes its amount off, so the invoice
        // shows why less is owed than the charge.
        foreach (var adjustment in transaction.Events.Where(evt => evt.Type == TransactionEventType.Adjusted && evt.Amount > 0m))
        {
            lineItems.Add(new ReceiptLineItem
            {
                Description = DescribeAdjustment(adjustment),
                Quantity = 1,
                UnitAmount = -adjustment.Amount.Value,
                Amount = -adjustment.Amount.Value,
            });
        }

        var request = new ReceiptRequest
        {
            BilledToName = contact?.DisplayName,
            BilledToEmail = contact?.Email,
            Reference = string.IsNullOrEmpty(transaction.InvoiceNumber) ? transaction.ItemId : transaction.InvoiceNumber,
            SourceLabel = S["Invoice"].Value,
            // The document prints its date as is, so it is given in the site's time zone, not in UTC.
            IssuedAt = await DocumentDates.ToSiteTimeAsync(_localClock, transaction.CreatedUtc),
            Currency = currency,
            LineItems = lineItems,
            TaxAmount = transaction.TaxAmount,
            Total = transaction.TotalAmount - transaction.AmountAdjusted,
            Status = paid ? ReceiptStatus.Paid : ReceiptStatus.Due,
            Notes = notes.Count == 0 ? null : string.Join(' ', notes),
        };

        if (transaction.TaxAmount > 0m)
        {
            request.TaxLines.Add(new ReceiptTaxLine
            {
                Description = S["Tax"].Value,
                Amount = transaction.TaxAmount,
            });
        }

        var invoice = await _receiptService.BuildAsync(request);

        invoice.HeaderTitle = S["Invoice"].Value;

        return invoice;
    }

    private string DescribeAdjustment(TransactionEvent adjustment)
    {
        string kind = adjustment.AdjustmentKind switch
        {
            TransactionAdjustmentKind.Waiver => S["Waiver"],
            TransactionAdjustmentKind.Credit => S["Credit"],
            TransactionAdjustmentKind.WriteOff => S["Write-off"],
            _ => S["Discount"],
        };

        return string.IsNullOrWhiteSpace(adjustment.Reason)
            ? kind
            : S["{0}: {1}", kind, adjustment.Reason].Value;
    }

    private static string Format(decimal amount, string currency)
        => string.IsNullOrEmpty(currency) ? CurrencyScale.Format(amount, currency) : $"{currency} {CurrencyScale.Format(amount, currency)}";
}
