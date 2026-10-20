using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// Pays out to a vendor what it earned on one order.
/// </summary>
public interface IVendorPayoutProvider
{
    /// <summary>
    /// Sends <paramref name="amount"/> to the vendor for an order, from that order's payment when it can. Returns the
    /// provider's reference for the transfer, or the reason it failed.
    /// </summary>
    Task<(string TransferId, string Failure)> PayAsync(Vendor vendor, Order order, decimal amount, string currency, string idempotencyKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// What vendors earn: each paid sale of their products, less the store's commission, held in escrow until released,
/// with refunds taken back, then paid out order by order.
/// </summary>
public sealed class MarketplaceService
{
    private readonly IVendorStore _vendors;
    private readonly IVendorEarningStore _earnings;
    private readonly IVendorPayoutStore _payouts;
    private readonly IContentManager _contentManager;
    private readonly IOrderStore _orders;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IVendorPayoutProvider> _payoutProviders;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public MarketplaceService(
        IVendorStore vendors,
        IVendorEarningStore earnings,
        IVendorPayoutStore payouts,
        IContentManager contentManager,
        IOrderStore orders,
        ISiteService siteService,
        IEnumerable<IVendorPayoutProvider> payoutProviders,
        IClock clock,
        ILogger<MarketplaceService> logger)
    {
        _vendors = vendors;
        _earnings = earnings;
        _payouts = payouts;
        _contentManager = contentManager;
        _orders = orders;
        _siteService = siteService;
        _payoutProviders = payoutProviders;
        _clock = clock;
        _logger = logger;
    }

    public bool CanPayOut
        => _payoutProviders.Any();

    /// <summary>
    /// Records what each vendor earns on a paid order. Recording an order twice records nothing more.
    /// </summary>
    public async Task<int> RecordSaleAsync(Order order, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);

        if ((await _earnings.ListByOrderAsync(order.ItemId, cancellationToken)).Any(earning => earning.Kind == VendorEarningKind.Sale))
        {
            return 0;
        }

        var settings = await _siteService.GetSettingsAsync<MarketplaceSettings>();
        var now = _clock.UtcNow;
        var paidUtc = order.PaidUtc ?? now;
        var recorded = 0;

        foreach (var line in order.Lines ?? [])
        {
            var item = await _contentManager.GetAsync(line.ContentItemId, VersionOptions.Latest);

            if (item?.TryGet<VendorPart>(out var part) != true || string.IsNullOrEmpty(part.VendorId) ||
                await _vendors.FindByIdAsync(part.VendorId, cancellationToken) is not { } vendor)
            {
                continue;
            }

            var gross = MarketplaceLedger.GetLineGross(line.LineTotal, order.Subtotal, order.DiscountTotal);
            var percent = vendor.CommissionPercent ?? settings.CommissionPercent;
            var (commission, net) = MarketplaceLedger.Split(gross, percent);

            await _earnings.CreateAsync(new VendorEarning
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                VendorId = vendor.ItemId,
                OrderId = order.ItemId,
                OrderNumber = order.OrderNumber,
                LineId = line.LineId,
                Title = line.Title,
                Currency = order.Currency,
                Gross = gross,
                CommissionPercent = percent,
                Commission = commission,
                Net = net,
                Kind = VendorEarningKind.Sale,
                Status = VendorEarningStatus.Held,
                HoldUntilUtc = paidUtc.AddDays(Math.Max(0, settings.HoldDays)),

                // Goods wait until they're on their way; a service or a download only for the hold.
                AwaitsFulfillment = settings.ReleaseOnFulfillment && line.ProductType is ProductType.Good or ProductType.Undefined,
                CreatedUtc = now,
            }, cancellationToken);

            recorded++;
        }

        return recorded;
    }

    /// <summary>
    /// Takes back each vendor's share of a refund: an entry against what it earned on the order, released with it, or
    /// against its next payout when that was paid out already.
    /// </summary>
    public async Task RecordRefundAsync(Order order, OrderRefund refund, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(refund);

        var earnings = await _earnings.ListByOrderAsync(order.ItemId, cancellationToken);
        var sales = earnings.Where(earning => earning.Kind == VendorEarningKind.Sale).ToList();

        if (sales.Count == 0 || earnings.Any(earning => earning.Kind == VendorEarningKind.Refund && earning.LineId == refund.RefundId))
        {
            return;
        }

        var paid = order.Total ?? order.Subtotal;
        var now = _clock.UtcNow;

        foreach (var sale in sales)
        {
            var gross = MarketplaceLedger.GetRefundShare(sale.Gross, refund.Amount, paid);

            if (gross <= 0m)
            {
                continue;
            }

            var (commission, net) = MarketplaceLedger.Split(-gross, sale.CommissionPercent);

            await _earnings.CreateAsync(new VendorEarning
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                VendorId = sale.VendorId,
                OrderId = order.ItemId,
                OrderNumber = order.OrderNumber,

                // The refund's id, so the same refund is taken back once.
                LineId = refund.RefundId,
                Title = sale.Title,
                Currency = sale.Currency,
                Gross = -gross,
                CommissionPercent = sale.CommissionPercent,
                Commission = commission,
                Net = net,
                Kind = VendorEarningKind.Refund,

                // Held with its sale, or, when the sale was paid out, ready to come off the next payout.
                Status = sale.Status == VendorEarningStatus.Held ? VendorEarningStatus.Held : VendorEarningStatus.Available,
                HoldUntilUtc = sale.HoldUntilUtc,
                AwaitsFulfillment = sale.AwaitsFulfillment,
                CreatedUtc = now,
            }, cancellationToken);
        }
    }

    /// <summary>
    /// Releases what's held whose hold ended, and whose order is fulfilled when it waits for that.
    /// </summary>
    public async Task<int> ReleaseDueAsync(int take, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var released = 0;
        var fulfilled = new Dictionary<string, bool>(StringComparer.Ordinal);

        foreach (var earning in await _earnings.ListByStatusAsync(VendorEarningStatus.Held, now, take, cancellationToken))
        {
            if (earning.AwaitsFulfillment)
            {
                if (!fulfilled.TryGetValue(earning.OrderId, out var isFulfilled))
                {
                    // An order nothing in which ships, or a store that doesn't track shipping, has no fulfillment to wait for.
                    isFulfilled = await _orders.FindByIdAsync(earning.OrderId, cancellationToken) is { FulfillmentStatus: null or OrderFulfillmentStatus.Fulfilled };
                    fulfilled[earning.OrderId] = isFulfilled;
                }

                if (!isFulfilled)
                {
                    continue;
                }
            }

            await ReleaseAsync(earning, now, cancellationToken);
            released++;
        }

        return released;
    }

    /// <summary>
    /// Releases what a vendor earned on an order now, such as for a service delivered, whatever the hold.
    /// </summary>
    public async Task<int> ReleaseOrderAsync(string vendorId, string orderId, CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var held = (await _earnings.ListByOrderAsync(orderId, cancellationToken)).Where(earning => earning.VendorId == vendorId && earning.Status == VendorEarningStatus.Held).ToList();

        foreach (var earning in held)
        {
            await ReleaseAsync(earning, now, cancellationToken);
        }

        return held.Count;
    }

    /// <summary>
    /// Pays a vendor what's released, one transfer per order, from that order's payment. Returns how much was paid and
    /// why anything wasn't.
    /// </summary>
    public async Task<(decimal Paid, IReadOnlyList<string> Failures)> PayOutAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        var failures = new List<string>();

        if (_payoutProviders.FirstOrDefault() is not { } provider)
        {
            return (0m, ["No payout provider is enabled, such as Marketplace payouts with Stripe Connect."]);
        }

        if (vendor.Status != VendorStatus.Active || !vendor.PayoutsEnabled)
        {
            return (0m, ["The vendor can't be paid out yet: it's suspended, or its payout account isn't set up."]);
        }

        var paid = 0m;
        var now = _clock.UtcNow;

        foreach (var group in (await _earnings.ListAvailableAsync(vendor.ItemId, cancellationToken)).GroupBy(earning => (earning.OrderId, earning.Currency)))
        {
            var amount = Math.Round(group.Sum(earning => earning.Net), 2, MidpointRounding.AwayFromZero);

            // A refund bigger than what's left of the order waits for later sales to make up for it.
            if (amount <= 0m)
            {
                continue;
            }

            var order = await _orders.FindByIdAsync(group.Key.OrderId, cancellationToken);
            var payout = new VendorPayout
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                VendorId = vendor.ItemId,
                OrderId = group.Key.OrderId,
                OrderNumber = order?.OrderNumber ?? group.First().OrderNumber,
                Amount = amount,
                Currency = group.Key.Currency,
                EarningIds = [.. group.Select(earning => earning.ItemId)],
                CreatedUtc = now,
            };

            var (transferId, failure) = await provider.PayAsync(vendor, order, amount, group.Key.Currency, payout.ItemId, cancellationToken);

            payout.TransferId = transferId;
            payout.Failure = failure;
            payout.Status = failure is null ? VendorPayoutStatus.Paid : VendorPayoutStatus.Failed;

            await _payouts.CreateAsync(payout, cancellationToken);

            if (failure is not null)
            {
                failures.Add(failure);
                _logger.LogWarning("Order '{OrderNumber}' couldn't be paid out to vendor '{VendorId}': {Failure}", payout.OrderNumber, vendor.ItemId, failure);

                continue;
            }

            // Read again, since the transfer may have committed what was read before it.
            foreach (var earningId in payout.EarningIds)
            {
                if (await _earnings.FindByIdAsync(earningId, cancellationToken) is { } earning)
                {
                    earning.Status = VendorEarningStatus.PaidOut;
                    earning.PayoutId = payout.ItemId;

                    await _earnings.UpdateAsync(earning, cancellationToken);
                }
            }

            paid += amount;
        }

        return (paid, failures);
    }

    /// <summary>
    /// Pays out every active vendor whose payouts are set up, for the automatic payouts.
    /// </summary>
    public async Task PayOutAllAsync(CancellationToken cancellationToken = default)
    {
        foreach (var vendor in await _vendors.ListAllAsync(cancellationToken))
        {
            if (vendor.Status == VendorStatus.Active && vendor.PayoutsEnabled)
            {
                await PayOutAsync(vendor, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Returns a vendor's totals by currency: held, ready to pay out, and paid out.
    /// </summary>
    public static IList<(string Currency, decimal Held, decimal Available, decimal PaidOut)> GetTotals(IEnumerable<VendorEarning> earnings)
        => [.. earnings
            .GroupBy(earning => earning.Currency ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(group => (
                group.Key,
                group.Where(earning => earning.Status == VendorEarningStatus.Held).Sum(earning => earning.Net),
                group.Where(earning => earning.Status == VendorEarningStatus.Available).Sum(earning => earning.Net),
                group.Where(earning => earning.Status == VendorEarningStatus.PaidOut).Sum(earning => earning.Net)))];

    private async Task ReleaseAsync(VendorEarning earning, DateTime now, CancellationToken cancellationToken)
    {
        earning.Status = VendorEarningStatus.Available;
        earning.ReleasedUtc = now;

        await _earnings.UpdateAsync(earning, cancellationToken);
    }
}
