using System.Globalization;
using CrestApps.OrchardCore.BusinessDocuments;
using CrestApps.OrchardCore.BusinessDocuments.Models;
using CrestApps.OrchardCore.BusinessDocuments.Services;
using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Customers.Services;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Receipts;
using CrestApps.OrchardCore.Receipts.Core.Services;
using CrestApps.OrchardCore.Receipts.Models;
using CrestApps.OrchardCore.Transactions.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The default <see cref="ITransactionInvoiceBuilder"/>. The payment request carries the site's business details from the
/// receipt settings, shows the whole transaction (the receipts for the payments made against it are separate documents),
/// and is a business document of the kind <see cref="TransactionsConstants.BusinessDocumentKinds.PaymentRequest"/>, so a
/// site gives it its own document template.
/// </summary>
public sealed class TransactionInvoiceBuilder : ITransactionInvoiceBuilder
{
    private readonly IReceiptService _receiptService;
    private readonly ReceiptBusinessDocumentMapper _mapper;
    private readonly IBusinessDocumentTemplateManager _templateManager;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly ICustomerContactResolver _contactResolver;
    private readonly ILocalClock _localClock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionInvoiceBuilder"/> class.
    /// </summary>
    /// <param name="receiptService">The receipt service that applies the site's business details.</param>
    /// <param name="mapper">The mapper that lays a receipt out as a business document.</param>
    /// <param name="templateManager">The document templates.</param>
    /// <param name="shellFeaturesManager">The features manager, which tells whether the template tables exist yet.</param>
    /// <param name="contactResolver">The resolver for whom the payment request is addressed to.</param>
    /// <param name="localClock">The clock that turns the stored UTC time into the site's time zone.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public TransactionInvoiceBuilder(
        IReceiptService receiptService,
        ReceiptBusinessDocumentMapper mapper,
        IBusinessDocumentTemplateManager templateManager,
        IShellFeaturesManager shellFeaturesManager,
        ICustomerContactResolver contactResolver,
        ILocalClock localClock,
        IStringLocalizer<TransactionInvoiceBuilder> stringLocalizer)
    {
        _receiptService = receiptService;
        _mapper = mapper;
        _templateManager = templateManager;
        _shellFeaturesManager = shellFeaturesManager;
        _contactResolver = contactResolver;
        _localClock = localClock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<BusinessDocument> BuildAsync(Transaction transaction, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var currency = transaction.Currency;
        var paid = transaction.OutstandingAmount <= 0m;
        var notes = new List<string>();

        if (transaction.DueUtc.HasValue)
        {
            notes.Add(S["Due on {0:d}.", await DocumentDates.ToSiteDueDateAsync(_localClock, transaction.DueUtc.Value)].Value);
        }

        if (!paid && transaction.AmountPaid > 0m)
        {
            notes.Add(S["{0} has been received; {1} is still due.", CurrencyFormat.WithCode(transaction.AmountPaid, currency), CurrencyFormat.WithCode(transaction.OutstandingAmount, currency)].Value);
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

        // Each adjustment (a discount, waiver, credit or write-off) is a line that takes its amount off, so the payment
        // request shows why less is owed than the charge.
        foreach (var adjustment in transaction.Events.Where(evt => evt.Type == TransactionEventType.Adjusted && evt.Amount > 0m))
        {
            if (transaction.Events.Any(other => other.Type == TransactionEventType.Reversed && string.Equals(other.ReversesEventId, adjustment.Id, StringComparison.Ordinal)))
            {
                continue;
            }

            lineItems.Add(new ReceiptLineItem
            {
                Description = DescribeAdjustment(adjustment),
                Quantity = 1,
                UnitAmount = -adjustment.Amount.Value,
                Amount = -adjustment.Amount.Value,
            });
        }

        var (billedToName, billedToEmail) = await GetBilledToAsync(transaction, cancellationToken);

        var request = new ReceiptRequest
        {
            BilledToName = billedToName,
            BilledToEmail = billedToEmail,
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

        AddTaxLines(request, transaction);

        var receipt = await _receiptService.BuildAsync(request);
        var document = _mapper.Map(receipt);

        document.Kind = TransactionsConstants.BusinessDocumentKinds.PaymentRequest;
        document.Title = S["Invoice"].Value;
        document.Number = request.Reference;

        // The number heads the document, so it is not repeated among the fields.
        document.Fields = [.. document.Fields.Where(field => !string.Equals(field.Value, request.Reference, StringComparison.Ordinal))];

        // The template is read only once its tables exist: on a site upgraded before Business Documents is recorded as
        // enabled, the payment request keeps the receipt settings' look until then.
        if (await _shellFeaturesManager.IsFeatureEnabledAsync(BusinessDocumentsConstants.Features.Area))
        {
            await _templateManager.ApplyAsync(document, cancellationToken: cancellationToken);
        }

        return document;
    }

    // The payer the transaction names is who the request is addressed to, such as a business account; a transaction from
    // before payers were stored is addressed to its owner.
    private async Task<(string Name, string Email)> GetBilledToAsync(Transaction transaction, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(transaction.PayerContact?.Name) || !string.IsNullOrWhiteSpace(transaction.PayerContact?.Email))
        {
            return (transaction.PayerContact.Name, transaction.PayerContact.Email);
        }

        var contact = await _contactResolver.ResolveAsync(transaction.GetOwner(), transaction.GetGuestContact(), cancellationToken);

        return (contact?.DisplayName, contact?.Email);
    }

    // One line per tax rate the ledger recorded (its breakdown), or a single tax line for a transaction that only has a
    // tax amount.
    private void AddTaxLines(ReceiptRequest request, Transaction transaction)
    {
        var breakdown = (transaction.TaxLines ?? [])
            .Where(line => line is not null && line.TaxAmount != 0m)
            .GroupBy(line => (Name: string.IsNullOrWhiteSpace(line.TaxName) ? null : line.TaxName, line.Rate))
            .ToList();

        if (breakdown.Count > 0)
        {
            foreach (var group in breakdown)
            {
                var name = group.Key.Name ?? S["Tax"].Value;

                request.TaxLines.Add(new ReceiptTaxLine
                {
                    // The ledger keeps a rate as a fraction, such as 0.05 for 5%.
                    Description = group.Key.Rate > 0m
                        ? S["{0} ({1}%)", name, (group.Key.Rate * 100m).ToString("0.####", CultureInfo.CurrentCulture)].Value
                        : name,
                    Amount = group.Sum(line => line.TaxAmount),
                });
            }

            return;
        }

        if (transaction.TaxAmount > 0m)
        {
            request.TaxLines.Add(new ReceiptTaxLine
            {
                Description = S["Tax"].Value,
                Amount = transaction.TaxAmount,
            });
        }
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
}
