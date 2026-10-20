using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Lets a signed-in customer's store credit pay toward their checkouts, and gives refunds as store credit.
/// </summary>
public sealed class StoreCreditTenderProvider : ICheckoutTenderProvider, IOrderStoreCreditRefunder
{
    private readonly IStoreCreditStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly StoreCreditService _storeCredit;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public StoreCreditTenderProvider(
        IStoreCreditStore store,
        ICustomerStore customerStore,
        StoreCreditService storeCredit,
        IClock clock,
        IStringLocalizer<StoreCreditTenderProvider> stringLocalizer)
    {
        _store = store;
        _customerStore = customerStore;
        _storeCredit = storeCredit;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key
        => StoreCreditService.TenderKey;

    /// <summary>
    /// Returns the store credit the checkout's signed-in buyer holds in the checkout's currency, or
    /// <see langword="null"/>.
    /// </summary>
    public async Task<StoreCreditAccount> FindForCheckoutAsync(CheckoutFlow flow, string currency, CancellationToken cancellationToken = default)
    {
        // Store credit belongs to an account, so only a signed-in buyer spends it; a register's checkout is the staff
        // member's, not the buyer's.
        if (flow?.Session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId } session ||
            session.TryGet<CheckoutCounterSale>(out _) ||
            await _customerStore.FindByUserIdAsync(ownerId, cancellationToken) is not { } customer)
        {
            return null;
        }

        return await _store.FindAsync(customer.ItemId, currency, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var account = await FindForCheckoutAsync(context.Flow, context.Invoice.Currency, cancellationToken);

        // Only the buyer's own credit pays, whatever the checkout was told.
        if (account is null || account.Balance <= 0m || !context.References.Contains(account.ItemId, StringComparer.Ordinal))
        {
            return [];
        }

        return
        [
            new TenderLine
            {
                Reference = account.ItemId,
                Description = S["Store credit"],
                Amount = account.Balance,
            },
        ];
    }

    /// <inheritdoc/>
    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(reference, cancellationToken);

        return account is not null && string.Equals(account.Currency, currency, StringComparison.OrdinalIgnoreCase) ? account.Balance : null;
    }

    /// <inheritdoc/>
    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(reference, cancellationToken)
            ?? throw new InvalidOperationException($"The store credit account '{reference}' no longer exists.");

        account.ChangeBalance(-amount, S["Spent at checkout"], _clock.UtcNow, flow?.Session?.SessionId);

        await _store.UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        if (await _store.FindByIdAsync(reference, cancellationToken) is not { } account)
        {
            return;
        }

        account.ChangeBalance(amount, string.IsNullOrWhiteSpace(reason) ? S["Refunded"] : reason, _clock.UtcNow);

        await _store.UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> CanRefundAsync(Order order, CancellationToken cancellationToken = default)
        => !string.IsNullOrEmpty(order?.UserId) && await _customerStore.FindByUserIdAsync(order.UserId, cancellationToken) is not null;

    /// <inheritdoc/>
    public async Task<bool> RefundAsync(Order order, decimal amount, string reason, string actor, CancellationToken cancellationToken = default)
    {
        if (amount <= 0m || string.IsNullOrEmpty(order?.UserId) || await _customerStore.FindByUserIdAsync(order.UserId, cancellationToken) is not { } customer)
        {
            return false;
        }

        var account = await _storeCredit.ChangeAsync(
            customer.ItemId,
            order.Currency,
            amount,
            string.IsNullOrWhiteSpace(reason) ? S["Refund of order {0}", order.OrderNumber] : S["Refund of order {0}: {1}", order.OrderNumber, reason],
            actor,
            order.CheckoutSessionId,
            cancellationToken);

        return account is not null;
    }
}
