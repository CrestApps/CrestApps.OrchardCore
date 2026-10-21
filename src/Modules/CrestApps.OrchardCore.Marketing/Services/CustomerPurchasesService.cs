using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Marketing.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Services;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Marketing.Services;

/// <summary>
/// Keeps each customer's purchases: an order counts once, when it's paid, at its total in the store's currency, and
/// each refund comes off once.
/// </summary>
public sealed class CustomerPurchasesService
{
    public const int BackfillBatchSize = 200;

    private readonly ICustomerPurchasesStore _store;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly IShopperCurrency _shopperCurrency;
    private readonly IOrderStore _orderStore;
    private readonly ISession _session;

    public CustomerPurchasesService(
        ICustomerPurchasesStore store,
        ISiteService siteService,
        IClock clock,
        IShopperCurrency shopperCurrency,
        IOrderStore orderStore,
        ISession session)
    {
        _store = store;
        _siteService = siteService;
        _clock = clock;
        _shopperCurrency = shopperCurrency;
        _orderStore = orderStore;
        _session = session;
    }

    /// <summary>
    /// Counts a paid order for its customer, with the refunds it has. An order already counted, or without a customer,
    /// is left alone.
    /// </summary>
    public async Task RecordAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (string.IsNullOrEmpty(order.CustomerId))
        {
            return;
        }

        var purchases = await _store.FindByIdAsync(order.CustomerId, cancellationToken);
        var isNew = purchases is null;

        // A refund comes off an order already counted, whatever the order's status now.
        if (order.Status != OrderStatus.Paid && purchases?.OrderIds.Contains(order.ItemId) != true)
        {
            return;
        }

        purchases ??= new CustomerPurchases { ItemId = order.CustomerId };

        if (!await ApplyAsync(purchases, order, cancellationToken))
        {
            return;
        }

        purchases.ModifiedUtc = _clock.UtcNow;

        if (isNew)
        {
            await _store.CreateAsync(purchases, cancellationToken);
        }
        else
        {
            await _store.UpdateAsync(purchases, cancellationToken);
        }
    }

    /// <summary>
    /// Adds an order and its refunds to a customer's purchases. Returns whether anything changed.
    /// </summary>
    internal async Task<bool> ApplyAsync(CustomerPurchases purchases, Order order, CancellationToken cancellationToken)
    {
        var changed = false;
        var storeCurrency = (await _siteService.GetSettingsAsync<CheckoutSettings>()).Currency;

        if (!purchases.OrderIds.Contains(order.ItemId))
        {
            var placed = order.PaidUtc ?? order.CreatedUtc;

            purchases.OrderIds.Add(order.ItemId);

            if (purchases.OrderIds.Count > CustomerPurchases.MaxOrderIds)
            {
                purchases.OrderIds.RemoveAt(0);
            }

            purchases.OrderCount++;
            purchases.TotalSpent += await ToStoreCurrencyAsync(order.Total ?? order.Subtotal, order.Currency, storeCurrency, cancellationToken);
            purchases.Email = order.CustomerEmail ?? purchases.Email;

            if (purchases.FirstOrderUtc is null || placed < purchases.FirstOrderUtc)
            {
                purchases.FirstOrderUtc = placed;
            }

            if (purchases.LastOrderUtc is null || placed >= purchases.LastOrderUtc)
            {
                purchases.LastOrderUtc = placed;
                purchases.Country = (order.BillingAddress?.Country ?? order.ShippingAddress?.Country)?.ToUpperInvariant() ?? purchases.Country;
            }

            changed = true;
        }

        foreach (var refund in order.Refunds ?? [])
        {
            if (string.IsNullOrEmpty(refund.RefundId) || purchases.RefundIds.Contains(refund.RefundId))
            {
                continue;
            }

            purchases.RefundIds.Add(refund.RefundId);
            purchases.TotalSpent = Math.Max(0m, purchases.TotalSpent - await ToStoreCurrencyAsync(refund.Amount, refund.Currency ?? order.Currency, storeCurrency, cancellationToken));
            changed = true;
        }

        return changed;
    }

    /// <summary>
    /// Counts the next batch of paid orders made before the feature was turned on. Returns whether there's more.
    /// </summary>
    public async Task<bool> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var state = await _session.Query<PurchasesBackfill>(collection: MarketingConstants.CollectionName).FirstOrDefaultAsync(cancellationToken)
            ?? new PurchasesBackfill { ItemId = PurchasesBackfill.Id };

        if (state.CompletedUtc is not null)
        {
            return false;
        }

        // Newest first: orders paid since only push older ones further down, so none is skipped, and counting is
        // idempotent, so one read twice isn't counted twice.
        var (_, orders) = await _orderStore.PageAsync(new OrderQuery
        {
            Statuses = [OrderStatus.Paid],
        }, state.Offset, BackfillBatchSize, cancellationToken);

        foreach (var order in orders)
        {
            await RecordAsync(order, cancellationToken);
        }

        state.Offset += orders.Count;

        if (orders.Count < BackfillBatchSize)
        {
            state.CompletedUtc = _clock.UtcNow;
        }

        await _session.SaveAsync(state, checkConcurrency: false, collection: MarketingConstants.CollectionName, cancellationToken: cancellationToken);

        return state.CompletedUtc is null;
    }

    private async Task<decimal> ToStoreCurrencyAsync(decimal amount, string currency, string storeCurrency, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(currency) || string.IsNullOrEmpty(storeCurrency) || string.Equals(currency, storeCurrency, StringComparison.OrdinalIgnoreCase))
        {
            return amount;
        }

        return await _shopperCurrency.ConvertAsync(amount, currency, storeCurrency, isPrice: false, cancellationToken) is { } converted
            ? converted
            : amount;
    }
}
