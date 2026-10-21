using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionReceiptBuilder"/>. A receipt is for the money in one payment, not for the
/// transaction as a whole: a balance paid in three parts gets three receipts, each showing what that part paid.
/// </summary>
public sealed class TransactionReceiptBuilder : ITransactionReceiptBuilder
{
    private readonly IReceiptService _receiptService;
    private readonly ICustomerContactResolver _contactResolver;
    private readonly IServiceProvider _serviceProvider;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionReceiptBuilder"/> class.
    /// </summary>
    /// <param name="receiptService">The receipt service that applies the site's receipt branding.</param>
    /// <param name="contactResolver">The resolver for whom the receipt is addressed to.</param>
    /// <param name="serviceProvider">The service provider the optional payment ledger is resolved from.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionReceiptBuilder(
        IReceiptService receiptService,
        ICustomerContactResolver contactResolver,
        IServiceProvider serviceProvider,
        IStringLocalizer<TransactionReceiptBuilder> stringLocalizer)
    {
        _receiptService = receiptService;
        _contactResolver = contactResolver;
        _serviceProvider = serviceProvider;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<ReceiptDocument> BuildAsync(Transaction transaction, TransactionEvent payment, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentNullException.ThrowIfNull(payment);

        if (payment.Type != TransactionEventType.PaymentRecorded || payment.Amount is not decimal paid || paid <= 0m)
        {
            return null;
        }

        var currency = transaction.Currency;
        var taxAmount = GetTaxShare(transaction, paid);

        var contact = await _contactResolver.ResolveAsync(transaction.GetOwner(), transaction.GetGuestContact(), cancellationToken);

        var request = new ReceiptRequest
        {
            BilledToName = contact?.DisplayName,
            BilledToEmail = contact?.Email,
            Reference = !string.IsNullOrEmpty(payment.ReceiptNumber)
                ? payment.ReceiptNumber
                : string.IsNullOrEmpty(payment.Id) ? transaction.ItemId : $"{transaction.ItemId}-{payment.Id}",
            SourceLabel = S["Receipt"].Value,
            // The document prints its date as is, so it is given in the site's time zone, not in UTC.
            IssuedAt = await DocumentDates.ToSiteTimeAsync(_serviceProvider.GetService<ILocalClock>(), payment.CreatedUtc),
            Currency = currency,
            LineItems =
            [
                new ReceiptLineItem
                {
                    Description = string.IsNullOrEmpty(transaction.Title) ? S["Payment"].Value : transaction.Title,
                    Quantity = 1,
                    UnitAmount = paid - taxAmount,
                    Amount = paid - taxAmount,
                },
            ],
            TaxAmount = taxAmount,
            Total = paid,
            Status = ReceiptStatus.Paid,
            Notes = BuildNotes(transaction, payment),
        };

        if (taxAmount > 0m)
        {
            request.TaxLines.Add(new ReceiptTaxLine
            {
                Description = S["Tax"].Value,
                Amount = taxAmount,
            });
        }

        if (!string.IsNullOrEmpty(payment.PaymentAttemptId) && _serviceProvider.GetService<IPaymentAttemptStore>() is { } attemptStore)
        {
            var attempt = await attemptStore.FindByIdAsync(payment.PaymentAttemptId, cancellationToken);

            if (attempt is not null)
            {
                request.IsTest = attempt.GatewayMode != GatewayMode.Live;
                request.GatewayId = attempt.ProviderReference;
            }
        }

        return await _receiptService.BuildAsync(request);
    }

    private string BuildNotes(Transaction transaction, TransactionEvent payment)
    {
        var notes = new List<string>();

        if (string.Equals(payment.Method, TransactionsConstants.SettlementMethods.Offline, StringComparison.Ordinal))
        {
            notes.Add(S["Paid offline."].Value);
        }

        // A receipt for an invoiced payment names the invoice it settles.
        if (!string.IsNullOrEmpty(transaction.InvoiceNumber))
        {
            notes.Add(S["Payment for invoice {0}.", transaction.InvoiceNumber].Value);
        }

        // What was taken off the charge without being paid, so the receipt explains why it is less than the charge.
        if (transaction.AmountAdjusted > 0m)
        {
            notes.Add(S["{0} {1} of the {2} {3} charged was taken off without being paid (a discount, waiver, credit or write-off).", transaction.Currency, CurrencyScale.Format(transaction.AmountAdjusted, transaction.Currency), transaction.Currency, CurrencyScale.Format(transaction.TotalAmount, transaction.Currency)].Value);
        }

        return notes.Count == 0 ? null : string.Join(' ', notes);
    }

    // A transaction's tax was decided when it was raised. A payment of part of it carries the same share of tax as
    // of the total, so the receipts for a balance paid in parts add up to the transaction's tax.
    private static decimal GetTaxShare(Transaction transaction, decimal paid)
    {
        if (transaction.TaxAmount <= 0m || transaction.TotalAmount <= 0m)
        {
            return 0m;
        }

        if (paid >= transaction.TotalAmount)
        {
            return transaction.TaxAmount;
        }

        return CurrencyScale.Round(transaction.TaxAmount * paid / transaction.TotalAmount, transaction.Currency);
    }
}
