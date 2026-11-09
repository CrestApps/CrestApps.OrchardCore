using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Models;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Pays an account buyer's order on the account's terms: the deposit is paid at checkout, and the rest is owed on an
/// invoice due in the account's net days, on the ledger with its reminders. Refunding what went on account takes it off
/// the invoice.
/// </summary>
public sealed class AccountTermsTenderProvider : DeferredBalanceTenderProviderBase
{
    // The obligation an order's account invoice is recorded under, so completing a checkout twice owes once.
    internal const string TermsObligationId = "account-terms";

    private readonly AccountTermsService _terms;
    private readonly IB2BAccountStore _accounts;
    private readonly IOrderStore _orders;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public AccountTermsTenderProvider(
        AccountTermsService terms,
        IB2BAccountStore accounts,
        ITransactionManager transactions,
        ITransactionCommandService commands,
        IOrderStore orders,
        IClock clock,
        ILogger<AccountTermsTenderProvider> logger,
        IStringLocalizer<AccountTermsTenderProvider> stringLocalizer)
        : base(transactions, commands, clock, logger, stringLocalizer)
    {
        _terms = terms;
        _accounts = accounts;
        _orders = orders;
        _clock = clock;
        S = stringLocalizer;
    }

    public override string Key
        => B2BAccountsConstants.TermsTenderKey;

    protected override string ObligationId
        => TermsObligationId;

    public override async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var (account, amount) = await _terms.GetForCheckoutAsync(context.Flow, context.Invoice, cancellationToken);

        if (account is null || amount <= 0m)
        {
            return [];
        }

        // Only the buyer's own account's terms pay, for this checkout, whatever the checkout was told.
        var reference = AccountTermsService.ToReference(account.AccountId, context.Flow.Session.SessionId);

        if (!context.References.Contains(reference, StringComparer.Ordinal))
        {
            return [];
        }

        return
        [
            new TenderLine
            {
                Reference = reference,
                Description = S["On account, net {0}", account.Terms.NetDays],
                Amount = amount,
            },
        ];
    }

    public override async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var (accountId, _) = AccountTermsService.FromReference(reference);

        return await _accounts.FindByIdAsync(accountId, cancellationToken) is { IsEnabled: true, Terms.OffersTerms: true } account
            ? await _terms.GetAvailableCreditAsync(account, cancellationToken)
            : null;
    }

    protected override string GetSessionId(string reference)
        => AccountTermsService.FromReference(reference).SessionId;

    protected override async Task<DeferredBalanceDebit> PrepareDebitAsync(string reference, CheckoutSession session, decimal amount, string currency, CancellationToken cancellationToken)
    {
        var (accountId, _) = AccountTermsService.FromReference(reference);

        if (await _accounts.FindByIdAsync(accountId, cancellationToken) is not { } account)
        {
            return null;
        }

        var order = string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal)
            ? await _orders.FindByIdAsync(session.ReferenceId, cancellationToken)
            : null;

        return new DeferredBalanceDebit
        {
            Title = order is null
                ? S["{0} on account", account.Name].Value
                : string.IsNullOrWhiteSpace(order.PurchaseOrderNumber)
                    ? S["Order {0} on account ({1})", order.OrderNumber, account.Name].Value
                    : S["Order {0}, PO {1}, on account ({2})", order.OrderNumber, order.PurchaseOrderNumber, account.Name].Value,
            Source = B2BAccountsConstants.TransactionSource,

            // The invoice belongs to the account, whichever of its buyers placed the order, so the account's page finds
            // it and what the account owes stays the same when a buyer leaves.
            ParentReferenceType = AccountsConstants.ReferenceTypes.Account,
            ParentReferenceId = account.AccountId,
            DueUtc = _clock.UtcNow.AddDays(account.Terms.NetDays ?? 0),
            Message = S["Put on {0}'s account, due in {1} days.", account.Name, account.Terms.NetDays ?? 0].Value,
        };
    }
}
