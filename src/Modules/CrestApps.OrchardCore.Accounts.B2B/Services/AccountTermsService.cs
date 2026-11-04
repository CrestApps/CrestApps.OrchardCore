using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Accounts.B2B.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// An account's standing with the store: what it owes on its terms, what more it may owe, and what part of an order goes
/// on the account rather than paid at checkout.
/// </summary>
public sealed class AccountTermsService
{
    // The most an account with no credit limit can put on account, to stand for "no limit" in the ledger's sums.
    internal const decimal Unlimited = 1_000_000_000m;

    private readonly ITransactionStore _transactions;
    private readonly B2BAccountService _accounts;
    private readonly ISiteService _siteService;

    public AccountTermsService(ITransactionStore transactions, B2BAccountService accounts, ISiteService siteService)
    {
        _transactions = transactions;
        _accounts = accounts;
        _siteService = siteService;
    }

    /// <summary>
    /// Returns what an account owes on its terms, in the store's currency: the outstanding invoices whose parent
    /// reference is the account, whichever of its buyers placed the order, and whether or not they are still a member.
    /// </summary>
    public async Task<(decimal Owed, string Currency)> GetOwedAsync(B2BAccount account, CancellationToken cancellationToken = default)
    {
        var currency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        var owed = (await ListInvoicesAsync(account, cancellationToken))
            .Where(transaction =>
                transaction.Status is TransactionStatus.Outstanding or TransactionStatus.PartiallyPaid &&
                string.Equals(transaction.Currency, currency, StringComparison.OrdinalIgnoreCase))
            .Sum(transaction => transaction.OutstandingAmount);

        return (owed, currency);
    }

    /// <summary>
    /// Returns the invoices an account was given for what it bought on its terms, newest first.
    /// </summary>
    public async Task<IReadOnlyList<Transaction>> ListInvoicesAsync(B2BAccount account, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(account?.AccountId))
        {
            return [];
        }

        return [.. (await _transactions.GetByParentReferenceAsync(AccountsConstants.ReferenceTypes.Account, account.AccountId, cancellationToken))
            .Where(transaction => string.Equals(transaction.Source, B2BAccountsConstants.TransactionSource, StringComparison.Ordinal))
            .OrderByDescending(transaction => transaction.CreatedUtc)];
    }

    /// <summary>
    /// Returns what more an account may owe: its credit limit less what it owes, or no limit.
    /// </summary>
    public async Task<decimal> GetAvailableCreditAsync(B2BAccount account, CancellationToken cancellationToken = default)
    {
        if (account?.Terms?.CreditLimit is not { } limit)
        {
            return Unlimited;
        }

        var (owed, _) = await GetOwedAsync(account, cancellationToken);

        return Math.Max(0m, limit - owed);
    }

    /// <summary>
    /// Returns the account a checkout is paid for on account and the part of it that goes on the account, or nulls
    /// when it can't be: its buyer doesn't buy for an account with terms, it's a register's sale, or it's in another
    /// currency than the store's.
    /// </summary>
    public async Task<(B2BAccount Account, decimal Amount)> GetForCheckoutAsync(CheckoutFlow flow, CheckoutInvoice invoice, CancellationToken cancellationToken = default)
    {
        if (flow?.Session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId } session ||
            session.TryGet<CheckoutCounterSale>(out _) ||
            invoice is null ||
            await _accounts.GetForUserAsync(ownerId, cancellationToken) is not { Terms.OffersTerms: true } account)
        {
            return (null, 0m);
        }

        var storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        if (!string.Equals(invoice.Currency, storeCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return (null, 0m);
        }

        // The deposit is paid at checkout; the rest of the order goes on account, up to the credit left.
        var onAccount = Money.Round(invoice.GrandTotal * (100m - Math.Clamp(account.Terms.DepositPercent, 0m, 100m)) / 100m, invoice.Currency);

        return (account, Math.Min(onAccount, await GetAvailableCreditAsync(account, cancellationToken)));
    }

    /// <summary>
    /// Returns the reference a checkout's account payment is chosen and recorded by: the account and the checkout.
    /// </summary>
    public static string ToReference(string accountId, string sessionId)
        => $"{accountId}:{sessionId}";

    /// <summary>
    /// Returns the account and checkout a reference names.
    /// </summary>
    public static (string AccountId, string SessionId) FromReference(string reference)
    {
        var separator = reference?.IndexOf(':', StringComparison.Ordinal) ?? -1;

        return separator > 0 ? (reference[..separator], reference[(separator + 1)..]) : (null, null);
    }
}
