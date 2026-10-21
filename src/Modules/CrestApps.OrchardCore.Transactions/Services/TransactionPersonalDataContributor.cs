using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.PersonalData;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// Exports and anonymizes the ledger transactions a person owns, as an account holder or through the guest owners of
/// their checkouts.
/// </summary>
/// <remarks>
/// Anonymizing removes the guest contact, the description of the card a transaction is collected from, and the
/// person's emails and names from titles and history messages. The amounts, statuses, dates, invoice number and the
/// receipt numbers of its payments stay, because the business must keep them, so ledger totals do not change.
/// </remarks>
public sealed class TransactionPersonalDataContributor : ICommercePersonalDataContributor
{
    internal const int PageSize = 100;
    internal const int MaxTransactionsPerOwner = 10_000;

    private readonly ITransactionManager _transactionManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionPersonalDataContributor"/> class.
    /// </summary>
    /// <param name="transactionManager">The transaction ledger.</param>
    public TransactionPersonalDataContributor(ITransactionManager transactionManager)
    {
        _transactionManager = transactionManager;
    }

    /// <inheritdoc/>
    public async Task IdentifyAsync(CommercePersonalDataSubject subject, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(subject);

        foreach (var transaction in await GetTransactionsAsync(_transactionManager, subject, cancellationToken))
        {
            // The checkout that created the transaction, and the one that settled it, hold the person's contact too.
            subject.AddCheckoutSession(transaction.CheckoutSessionId);

            if (string.Equals(transaction.SettlementMethod, TransactionsConstants.SettlementMethods.Online, StringComparison.Ordinal))
            {
                subject.AddCheckoutSession(transaction.SettlementReference);
            }

            if (transaction.OwnerKind == CustomerOwnerKind.Guest)
            {
                subject.AddName(transaction.GuestContactName);
            }
        }
    }

    /// <inheritdoc/>
    public async Task ExportAsync(CommercePersonalDataExportContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var transaction in await GetTransactionsAsync(_transactionManager, context.Subject, cancellationToken))
        {
            var record = new CommerceTransactionRecord
            {
                ItemId = transaction.ItemId,
                Title = transaction.Title,
                Source = transaction.Source,
                Owner = transaction.GetOwner(),
                ReferenceType = transaction.ReferenceType,
                ReferenceId = transaction.ReferenceId,
                CheckoutSessionId = transaction.CheckoutSessionId,
                InvoiceNumber = transaction.InvoiceNumber,
                Status = transaction.Status.ToString(),
                Currency = transaction.Currency,
                Amount = transaction.Amount,
                TaxAmount = transaction.TaxAmount,
                TotalAmount = transaction.TotalAmount,
                AmountPaid = transaction.AmountPaid,
                AmountRefunded = transaction.AmountRefunded,
                CreatedUtc = transaction.CreatedUtc,
                DueUtc = transaction.DueUtc,
                SettledUtc = transaction.SettledUtc,
                ContactName = transaction.GuestContactName,
                ContactEmail = transaction.GuestContactEmail,
                PaymentMethodDescription = transaction.AutoCollection?.PaymentMethodDescription,
            };

            foreach (var receiptNumber in transaction.Events?.Select(item => item.ReceiptNumber).Where(number => !string.IsNullOrEmpty(number)) ?? [])
            {
                record.ReceiptNumbers.Add(receiptNumber);
            }

            context.Export.Transactions.Add(record);
        }
    }

    /// <inheritdoc/>
    public async Task AnonymizeAsync(CommercePersonalDataAnonymizeContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        foreach (var transaction in await GetTransactionsAsync(_transactionManager, context.Subject, cancellationToken))
        {
            Anonymize(transaction, context);

            await _transactionManager.UpdateAsync(transaction, data: null, cancellationToken);

            context.Result.Transactions++;
        }
    }

    /// <summary>
    /// Removes the personal data from one transaction, leaving every amount, status, date and document number as it
    /// was.
    /// </summary>
    internal static void Anonymize(Transaction transaction, CommercePersonalDataAnonymizeContext context)
    {
        transaction.GuestContactName = context.ReplaceName(transaction.GuestContactName);
        transaction.GuestContactEmail = null;
        transaction.Title = context.Scrub(transaction.Title);

        if (transaction.AutoCollection is not null)
        {
            transaction.AutoCollection.PaymentMethodDescription = null;
        }

        foreach (var item in transaction.Events ?? [])
        {
            item.Message = context.Scrub(item.Message);
            item.ActorName = context.Scrub(item.ActorName);
        }
    }

    /// <summary>
    /// Returns every transaction owned by one of the person's owners, oldest first.
    /// </summary>
    internal static async Task<IReadOnlyList<Transaction>> GetTransactionsAsync(ITransactionManager transactionManager, CommercePersonalDataSubject subject, CancellationToken cancellationToken)
    {
        var transactions = new Dictionary<string, Transaction>(StringComparer.Ordinal);

        foreach (var owner in subject.Owners.ToArray())
        {
            for (var page = 1; (page - 1) * PageSize < MaxTransactionsPerOwner; page++)
            {
                var result = await transactionManager.PageAsync(page, PageSize, new TransactionQuery
                {
                    OwnerId = owner.Id,
                    OwnerKind = owner.Kind,
                }, cancellationToken);

                var entries = result.Entries?.ToArray() ?? [];

                foreach (var transaction in entries)
                {
                    transactions.TryAdd(transaction.ItemId, transaction);
                }

                if (entries.Length < PageSize)
                {
                    break;
                }
            }
        }

        return [.. transactions.Values.OrderBy(transaction => transaction.CreatedUtc)];
    }
}
