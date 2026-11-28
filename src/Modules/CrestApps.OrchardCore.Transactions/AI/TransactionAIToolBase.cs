using CrestApps.OrchardCore.Currencies;
using CrestApps.OrchardCore.Transactions.Core;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security.Permissions;
using YesSql;

namespace CrestApps.OrchardCore.Transactions.AI;

/// <summary>
/// A tool of Commerce › Transactions: it needs <see cref="TransactionsPermissions.ManageTransactions"/>, as the
/// transactions list and a transaction's page.
/// </summary>
internal abstract class TransactionAIToolBase : TransactionsAIToolBase
{
    /// <inheritdoc/>
    protected override Permission Permission => TransactionsPermissions.ManageTransactions;

    /// <summary>
    /// Finds a transaction by the <c>transactionId</c> argument.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="transactionId">The transaction's id.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The transaction, or the sentence that says why there is none.</returns>
    protected static async Task<(Transaction Transaction, string Error)> FindTransactionAsync(IServiceProvider services, string transactionId, CancellationToken cancellationToken)
    {
        if (transactionId is null)
        {
            return (null, "Nothing was done: give the transaction's id. Use listTransactions to find it.");
        }

        return await services.GetRequiredService<ITransactionManager>().FindByIdAsync(transactionId, cancellationToken) is { } transaction
            ? (transaction, null)
            : (null, $"Nothing was done: no transaction has the id '{transactionId}'. Use listTransactions to find it.");
    }

    /// <summary>
    /// Checks a money amount for a transaction: more than zero, in the transaction's currency and within its decimal places.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="amount">The amount given.</param>
    /// <param name="currency">The currency given.</param>
    /// <param name="verb">What was not done, such as "recorded".</param>
    /// <returns><see langword="null"/> when the amount is fine, or the sentence that says why not.</returns>
    protected static string CheckAmount(Transaction transaction, decimal? amount, string currency, string verb)
    {
        if (amount is not > 0m)
        {
            return $"Nothing was {verb}: give an amount greater than zero.";
        }

        if (!string.Equals(currency, transaction.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return $"Nothing was {verb}: the transaction is in {transaction.Currency}; give the amount in {transaction.Currency}.";
        }

        return CurrencyScale.Round(amount.Value, transaction.Currency) != amount.Value
            ? $"Nothing was {verb}: {Invariant(amount.Value)} has more decimal places than {transaction.Currency} allows."
            : null;
    }

    /// <summary>
    /// Gets what is still owed, at the currency's own scale.
    /// </summary>
    /// <param name="transaction">The transaction.</param>
    /// <returns>The amount still owed.</returns>
    protected static decimal Outstanding(Transaction transaction)
        => CurrencyScale.Round(transaction.OutstandingAmount, transaction.Currency);

    /// <summary>
    /// Saves a change made on a transaction outside the command service and commits it, as the transaction's page does,
    /// turning a change made by someone else at the same moment into a sentence.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="null"/> when it was saved, or the sentence that says why not.</returns>
    protected static async Task<string> SaveAsync(IServiceProvider services, Transaction transaction, CancellationToken cancellationToken)
    {
        var session = services.GetRequiredService<ISession>();

        try
        {
            await services.GetRequiredService<ITransactionManager>().UpdateAsync(transaction, data: null, cancellationToken);
            await session.SaveChangesAsync(cancellationToken);

            return null;
        }
        catch (ConcurrencyException)
        {
            await session.CancelAsync();

            return "Nothing was saved: this transaction was changed by someone else at the same moment. Read it again with getTransaction and retry in your next message.";
        }
    }

    /// <summary>
    /// Builds the allow-listed fields of a transaction for a list.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <param name="transaction">The transaction.</param>
    /// <returns>The fields.</returns>
    protected static async Task<Dictionary<string, object>> DescribeAsync(IServiceProvider services, Transaction transaction)
        => new()
        {
            ["id"] = transaction.ItemId,
            ["title"] = transaction.Title,
            ["source"] = transaction.Source,
            ["status"] = ToCamel(transaction.Status.ToString()),
            ["currency"] = transaction.Currency,
            ["total"] = transaction.TotalAmount,
            ["paid"] = transaction.AmountPaid,
            ["adjusted"] = transaction.AmountAdjusted,
            ["outstanding"] = Outstanding(transaction),
            ["refunded"] = transaction.AmountRefunded,
            ["dueAt"] = await LocalTimeAsync(services, transaction.DueUtc),
            ["invoiceNumber"] = transaction.InvoiceNumber,
            ["createdAt"] = await LocalTimeAsync(services, transaction.CreatedUtc),
        };
}
