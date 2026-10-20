using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Customers.Services;

/// <summary>
/// Lets a signed-in customer's loyalty points pay toward their checkouts, at what the program says a point is worth.
/// </summary>
public sealed class LoyaltyTenderProvider : ICheckoutTenderProvider
{
    private readonly ILoyaltyStore _store;
    private readonly ICustomerStore _customerStore;
    private readonly LoyaltyService _loyalty;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public LoyaltyTenderProvider(
        ILoyaltyStore store,
        ICustomerStore customerStore,
        LoyaltyService loyalty,
        IClock clock,
        IStringLocalizer<LoyaltyTenderProvider> stringLocalizer)
    {
        _store = store;
        _customerStore = customerStore;
        _loyalty = loyalty;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key
        => LoyaltyService.TenderKey;

    /// <summary>
    /// Returns the points of the checkout's signed-in buyer, or <see langword="null"/>.
    /// </summary>
    public async Task<LoyaltyAccount> FindForCheckoutAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        // A register's checkout is the staff member's, not the buyer's.
        if (flow?.Session is not CheckoutSession { OwnerId: { Length: > 0 } ownerId } session ||
            session.TryGet<CheckoutCounterSale>(out _) ||
            await _customerStore.FindByUserIdAsync(ownerId, cancellationToken) is not { } customer)
        {
            return null;
        }

        return await _store.FindByCustomerAsync(customer.ItemId, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var account = await FindForCheckoutAsync(context.Flow, cancellationToken);
        var settings = await _loyalty.GetSettingsAsync();

        if (account is null || account.Points < Math.Max(1, settings.MinimumPointsToRedeem) || !context.References.Contains(account.ItemId, StringComparer.Ordinal))
        {
            return [];
        }

        var value = LoyaltyService.GetValue(account.Points, settings, context.Invoice.Currency);

        return value <= 0m
            ? []
            : [new TenderLine { Reference = account.ItemId, Description = settings.ProgramName, Amount = value }];
    }

    /// <inheritdoc/>
    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(reference, cancellationToken);

        return account is null ? null : LoyaltyService.GetValue(account.Points, await _loyalty.GetSettingsAsync(), currency);
    }

    /// <inheritdoc/>
    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var account = await _store.FindByIdAsync(reference, cancellationToken)
            ?? throw new InvalidOperationException($"The loyalty account '{reference}' no longer exists.");
        var points = Math.Min(account.Points, LoyaltyService.GetPointsWorth(amount, await _loyalty.GetSettingsAsync()));

        account.ChangePoints(-points, LoyaltyActivityKind.Spent, S["Spent at checkout"], _clock.UtcNow, sessionId: flow?.Session?.SessionId);

        await _store.UpdateAsync(account, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        if (await _store.FindByIdAsync(reference, cancellationToken) is not { } account)
        {
            return;
        }

        var points = LoyaltyService.GetPointsWorth(amount, await _loyalty.GetSettingsAsync());

        account.ChangePoints(points, LoyaltyActivityKind.Returned, string.IsNullOrWhiteSpace(reason) ? S["Refunded"] : reason, _clock.UtcNow);

        await _store.UpdateAsync(account, cancellationToken);
    }
}
