using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Builds the invoice for a transaction its owner pays themselves.
/// </summary>
public interface ITransactionInvoiceBuilder
{
    /// <summary>
    /// Builds the invoice for <paramref name="transaction"/>: what it is for, its tax, its total, and what is still due.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<ReceiptDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default);
}

/// <summary>
/// The default <see cref="ITransactionInvoiceBuilder"/>. An invoice uses the site's receipt branding and layout, and
/// shows the whole transaction: the receipts for the payments made against it are separate documents.
/// </summary>
public sealed class TransactionInvoiceBuilder : ITransactionInvoiceBuilder
{
    private readonly IReceiptService _receiptService;
    private readonly ICustomerContactResolver _contactResolver;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceBuilder"/> class.
    /// </summary>
    /// <param name="receiptService">The receipt service that applies the site's branding.</param>
    /// <param name="contactResolver">The resolver for whom the invoice is addressed to.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionInvoiceBuilder(
        IReceiptService receiptService,
        ICustomerContactResolver contactResolver,
        IStringLocalizer<TransactionInvoiceBuilder> stringLocalizer)
    {
        _receiptService = receiptService;
        _contactResolver = contactResolver;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<ReceiptDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var currency = transaction.Currency;
        var owner = transaction.OwnerKind == CustomerOwnerKind.Guest
            ? CustomerOwner.ForGuest(transaction.OwnerId)
            : CustomerOwner.ForUser(transaction.OwnerId);

        var contact = await _contactResolver.ResolveAsync(
            owner,
            new CustomerContact
            {
                DisplayName = transaction.GuestContactName,
                Email = transaction.GuestContactEmail,
            },
            cancellationToken);

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

        var request = new ReceiptRequest
        {
            BilledToName = contact?.DisplayName,
            BilledToEmail = contact?.Email,
            Reference = string.IsNullOrEmpty(transaction.InvoiceNumber) ? transaction.ItemId : transaction.InvoiceNumber,
            SourceLabel = S["Invoice"].Value,
            IssuedAt = transaction.CreatedUtc,
            Currency = currency,
            LineItems =
            [
                new ReceiptLineItem
                {
                    Description = string.IsNullOrEmpty(transaction.Title) ? S["Payment"].Value : transaction.Title,
                    Quantity = 1,
                    UnitAmount = transaction.TotalAmount - transaction.TaxAmount,
                    Amount = transaction.TotalAmount - transaction.TaxAmount,
                },
            ],
            TaxAmount = transaction.TaxAmount,
            Total = transaction.TotalAmount,
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

    private static string Format(decimal amount, string currency)
        => string.IsNullOrEmpty(currency) ? CurrencyScale.Format(amount, currency) : $"{currency} {CurrencyScale.Format(amount, currency)}";
}
