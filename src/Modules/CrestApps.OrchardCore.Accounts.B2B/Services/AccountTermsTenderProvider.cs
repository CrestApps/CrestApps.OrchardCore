using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Transactions;
using CrestApps.OrchardCore.Transactions.Models;
using CrestApps.OrchardCore.Transactions.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Accounts.B2B.Services;

/// <summary>
/// Pays an account buyer's order on the account's terms: the deposit is paid at checkout, and the rest is owed on an
/// invoice due in the account's net days, on the ledger with its reminders. Refunding what went on account takes it off
/// the invoice.
/// </summary>
public sealed class AccountTermsTenderProvider : ICheckoutTenderProvider
{
    // The obligation an order's account invoice is recorded under, so completing a checkout twice owes once.
    internal const string ObligationId = "account-terms";

    private readonly AccountTermsService _terms;
    private readonly IB2BAccountStore _accounts;
    private readonly ITransactionManager _transactions;
    private readonly ITransactionCommandService _commands;
    private readonly IOrderStore _orders;
    private readonly IClock _clock;
    private readonly ILogger _logger;

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
    {
        _terms = terms;
        _accounts = accounts;
        _transactions = transactions;
        _commands = commands;
        _orders = orders;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public string Key
        => B2BAccountsConstants.TermsTenderKey;

    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
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

    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var (accountId, _) = AccountTermsService.FromReference(reference);

        return await _accounts.FindByIdAsync(accountId, cancellationToken) is { IsEnabled: true, Terms.OffersTerms: true } account
            ? await _terms.GetAvailableCreditAsync(account, cancellationToken)
            : null;
    }

    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var (accountId, sessionId) = AccountTermsService.FromReference(reference);

        if (flow?.Session is not CheckoutSession session ||
            !string.Equals(session.SessionId, sessionId, StringComparison.Ordinal) ||
            await _accounts.FindByIdAsync(accountId, cancellationToken) is not { } account)
        {
            throw new InvalidOperationException($"The account payment '{reference}' doesn't belong to this checkout.");
        }

        if (await _transactions.GetByObligationAsync(session.SessionId, ObligationId, cancellationToken) is not null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var order = string.Equals(session.ReferenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal)
            ? await _orders.FindByIdAsync(session.ReferenceId, cancellationToken)
            : null;
        var transaction = await _transactions.NewAsync(cancellationToken: cancellationToken);

        transaction.Title = order is null
            ? S["{0} on account", account.Name].Value
            : string.IsNullOrWhiteSpace(order.PurchaseOrderNumber)
                ? S["Order {0} on account ({1})", order.OrderNumber, account.Name].Value
                : S["Order {0}, PO {1}, on account ({2})", order.OrderNumber, order.PurchaseOrderNumber, account.Name].Value;
        transaction.Source = B2BAccountsConstants.TransactionSource;
        transaction.OwnerId = session.OwnerId;
        transaction.OwnerKind = CustomerOwnerKind.Authenticated;
        transaction.ReferenceType = session.ReferenceType;
        transaction.ReferenceId = session.ReferenceId;
        transaction.ReferenceVersionId = session.ReferenceVersionId;
        transaction.CheckoutSessionId = session.SessionId;
        transaction.ObligationId = ObligationId;
        transaction.Currency = currency;
        transaction.Amount = amount;
        transaction.TaxAmount = 0m;
        transaction.TotalAmount = amount;
        transaction.AmountPaid = 0m;
        transaction.Status = TransactionStatus.Outstanding;
        transaction.CreatedUtc = now;
        transaction.UpdatedUtc = now;
        transaction.DueUtc = now.AddDays(account.Terms.NetDays ?? 0);
        transaction.Events.Add(new TransactionEvent
        {
            CreatedUtc = now,
            Type = TransactionEventType.Created,
            Message = S["Put on {0}'s account, due in {1} days.", account.Name, account.Terms.NetDays ?? 0].Value,
        });

        await _transactions.CreateAsync(transaction, cancellationToken);
    }

    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        var (_, sessionId) = AccountTermsService.FromReference(reference);

        if (amount <= 0m || string.IsNullOrEmpty(sessionId) ||
            await _transactions.GetByObligationAsync(sessionId, ObligationId, cancellationToken) is not { } transaction)
        {
            return;
        }

        // What's refunded comes off what's still owed; what was already paid on the invoice is refunded by the store.
        var owed = transaction.OutstandingAmount;
        var adjustment = Math.Min(amount, owed);

        if (adjustment <= 0m)
        {
            _logger.LogWarning("A refund of {Amount} went to the account of checkout '{SessionId}', whose invoice was already paid; refund the account directly.", amount, sessionId);

            return;
        }

        var result = await _commands.AdjustAsync(transaction.ItemId, adjustment, TransactionAdjustmentKind.Waiver, string.IsNullOrWhiteSpace(reason) ? S["Refunded"].Value : reason, cancellationToken);

        if (!result.Succeeded)
        {
            _logger.LogWarning("The account invoice '{TransactionId}' couldn't be lowered by a refund: {Errors}", transaction.ItemId, string.Join(" ", result.Errors));
        }
    }
}
