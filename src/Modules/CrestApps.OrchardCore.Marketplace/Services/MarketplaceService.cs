using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Banking.Disbursements.Models;
using CrestApps.OrchardCore.Banking.Disbursements.Services;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.Marketplace.Telemetry;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Marketplace.Services;

/// <summary>
/// A Disbursements provider that can pay a vendor: the disbursement's payee is the vendor
/// (<see cref="MarketplaceConstants.VendorPartyKind"/>) and its source the order whose earnings it pays
/// (<see cref="MarketplaceConstants.PayoutSourceKind"/>). Vendor payouts are disbursements, so they are batched,
/// released, listed and reported by the Disbursements building block.
/// </summary>
public interface IVendorPayoutProvider : IDisbursementProvider
{
    /// <summary>
    /// Looks for the transfer an earlier submission of a payout's <paramref name="disbursement"/> made, when that
    /// submission's outcome isn't known (the disbursement is still submitted, or was settled without a trace number by
    /// the generic settle path). It is asked before the payout is given up on or recorded paid, because the provider may no longer hold its idempotency key (Stripe keeps one for 24 hours).
    /// </summary>
    /// <param name="disbursement">The payout's disbursement.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The transfer when one exists, <see cref="VendorPayoutResult.Unsettled"/> when the provider can't be asked
    /// now, or <see langword="null"/> when no transfer exists.</returns>
    Task<VendorPayoutResult> FindTransferAsync(Disbursement disbursement, CancellationToken cancellationToken = default);
}

/// <summary>
/// What vendors earn: each paid sale of their products, less the store's commission, held in escrow until released,
/// with refunds taken back, then paid out order by order.
/// </summary>
public sealed partial class MarketplaceService
{
    // How many of a vendor's latest payouts are looked through for one still pending.
    private const int PendingPayoutLookback = 100;

    private readonly IVendorStore _vendors;
    private readonly IVendorEarningStore _earnings;
    private readonly IVendorPayoutStore _payouts;
    private readonly IContentManager _contentManager;
    private readonly IOrderStore _orders;
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IVendorPayoutProvider> _payoutProviders;
    private readonly MarketplacePayoutServices _payoutServices;
    private readonly ILocalClock _localClock;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    public MarketplaceService(
        IVendorStore vendors,
        IVendorEarningStore earnings,
        IVendorPayoutStore payouts,
        IContentManager contentManager,
        IOrderStore orders,
        ISiteService siteService,
        IEnumerable<IVendorPayoutProvider> payoutProviders,
        MarketplacePayoutServices payoutServices,
        ILocalClock localClock,
        IClock clock,
        ILogger<MarketplaceService> logger,
        IStringLocalizer<MarketplaceService> stringLocalizer)
    {
        _vendors = vendors;
        _earnings = earnings;
        _payouts = payouts;
        _contentManager = contentManager;
        _orders = orders;
        _siteService = siteService;
        _payoutProviders = payoutProviders;
        _payoutServices = payoutServices;
        _localClock = localClock;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    public bool CanPayOut
        => _payoutProviders.Any() && _payoutServices.CanDisburse;

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
            var item = await _contentManager.GetPurchasedAsync(line);

            if (item?.TryGet<VendorPart>(out var part) != true || string.IsNullOrEmpty(part.VendorId) ||
                await _vendors.FindByIdAsync(part.VendorId, cancellationToken) is not { } vendor)
            {
                continue;
            }

            var gross = MarketplaceLedger.GetLineGross(line.LineTotal, order.Subtotal, order.DiscountTotal, order.Currency);
            var percent = vendor.CommissionPercent ?? settings.CommissionPercent;
            var (commission, net) = MarketplaceLedger.Split(gross, percent, order.Currency);

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
            var gross = MarketplaceLedger.GetRefundShare(sale.Gross, refund.Amount, paid, sale.Currency);

            if (gross <= 0m)
            {
                continue;
            }

            var (commission, net) = MarketplaceLedger.Split(-gross, sale.CommissionPercent, sale.Currency);

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
    /// Pays a vendor what's released, one transfer per order, from that order's payment, each as a disbursement. What a
    /// refund took back after the order was paid out comes off the vendor's next transfers. Returns how much was paid and
    /// why anything wasn't.
    /// </summary>
    public async Task<(decimal Paid, IReadOnlyList<string> Failures)> PayOutAsync(Vendor vendor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vendor);

        var settings = await _siteService.GetSettingsAsync<MarketplaceSettings>();

        if (SelectPayoutProvider(_payoutProviders, settings.PayoutProvider) is not { } provider)
        {
            return (0m, [_payoutProviders.Any()
                ? S["Choose the payout provider in the marketplace settings: more than one is enabled, or the one chosen is not."].Value
                : S["No payout provider is enabled, such as Marketplace payouts with Stripe Connect."].Value]);
        }

        if (vendor.Status != VendorStatus.Active || !vendor.PayoutsEnabled)
        {
            return (0m, ["The vendor can't be paid out yet: it's suspended, or its payout account isn't set up."]);
        }

        if (!_payoutServices.CanDisburse)
        {
            return (0m, [S["Turn on Disbursements: vendor payouts are sent as disbursements."].Value]);
        }

        // One payout of a vendor at a time, on every node, committed before the next one reads the earnings, so two
        // payouts started together never send the same earnings twice.
        var (locker, locked) = await _payoutServices.DistributedLock.TryAcquireLockAsync($"Marketplace:Payout:{vendor.ItemId}", TimeSpan.FromSeconds(30), TimeSpan.FromMinutes(10));

        if (!locked)
        {
            return (0m, [S["A payout of this vendor is already running. Try again in a moment."].Value]);
        }

        try
        {
            return await PayOutLockedAsync(vendor, provider, _payoutServices.Disbursements, cancellationToken);
        }
        finally
        {
            if (locker is not null)
            {
                await locker.DisposeAsync();
            }
        }
    }

    private async Task<(decimal Paid, IReadOnlyList<string> Failures)> PayOutLockedAsync(Vendor vendor, IVendorPayoutProvider provider, IDisbursementService disbursements, CancellationToken cancellationToken)
    {
        var failures = new List<string>();
        var paid = 0m;
        var now = _clock.UtcNow;
        var today = DateOnly.FromDateTime((await _localClock.GetLocalNowAsync()).DateTime);
        var available = await _earnings.ListAvailableAsync(vendor.ItemId, cancellationToken);
        var settled = new HashSet<string>(StringComparer.Ordinal);

        // A payout recorded before its transfer and never marked paid (the run stopped in between) is finished first, sent
        // again as it was recorded (amount, earnings and key), so its disbursement answers with the transfer it already
        // made. Planning around it instead would put its earnings in a new transfer with a new key whenever anything
        // changed meanwhile, such as a refund of the same order, and pay them twice.
        foreach (var payout in await ListPendingAsync(vendor, available, cancellationToken))
        {
            settled.UnionWith(payout.EarningIds ?? []);

            // Only the provider it was recorded with knows whether its transfer was made.
            if (GetRecordedProvider(payout, provider) is not { } recorded)
            {
                failures.Add(S["The payout of order {0} was started with {1}, which isn't enabled now. It stays pending until it is.", payout.OrderNumber, payout.Provider].Value);

                continue;
            }

            paid += await SendAsync(disbursements, vendor, recorded, payout, today, resend: true, failures, cancellationToken);
        }

        foreach (var batch in PlanPayouts(available.Where(earning => !settled.Contains(earning.ItemId))))
        {
            var order = await _orders.FindByIdAsync(batch.OrderId, cancellationToken);
            var earningIds = batch.Earnings.Select(earning => earning.ItemId).ToList();

            var payout = new VendorPayout
            {
                ItemId = global::OrchardCore.IdGenerator.GenerateId(),
                VendorId = vendor.ItemId,
                OrderId = batch.OrderId,
                OrderNumber = order?.OrderNumber ?? batch.Earnings[0].OrderNumber,
                Amount = batch.Amount,
                Currency = batch.Currency,
                EarningIds = earningIds,
                Provider = provider.Name,
                Status = VendorPayoutStatus.Pending,
                CreatedUtc = now,

                // Stable for the same vendor, order and earnings, and the disbursement's key: a retry is answered with the
                // same disbursement and the same transfer.
                IdempotencyKey = MarketplaceLedger.GetPayoutKey(vendor.ItemId, batch.OrderId, earningIds, batch.Earnings.Max(earning => earning.RefusedPayouts)),
            };

            await _payouts.CreateAsync(payout, cancellationToken);

            // The earnings name the payout while it is pending, so the next run finds it however many payouts came after.
            foreach (var earningId in earningIds)
            {
                if (await _earnings.FindByIdAsync(earningId, cancellationToken) is { } earning)
                {
                    earning.PayoutId = payout.ItemId;

                    await _earnings.UpdateAsync(earning, cancellationToken);
                }
            }

            // Committed before the disbursement is sent, so the key is on record whatever happens next.
            await CommitAsync(cancellationToken);

            paid += await SendAsync(disbursements, vendor, provider, payout, today, resend: false, failures, cancellationToken);
        }

        await CommitAsync(cancellationToken);

        return (paid, failures);
    }

    private Task CommitAsync(CancellationToken cancellationToken)
        => _payoutServices.Session.SaveChangesAsync(cancellationToken);

    // The provider a pending payout was recorded with, or null when it isn't enabled now.
    private IVendorPayoutProvider GetRecordedProvider(VendorPayout payout, IVendorPayoutProvider selected)
        => string.IsNullOrEmpty(payout.Provider) || string.Equals(payout.Provider, selected.Name, StringComparison.OrdinalIgnoreCase)
            ? selected
            : _payoutProviders.FirstOrDefault(candidate => string.Equals(candidate.Name, payout.Provider, StringComparison.OrdinalIgnoreCase));

    // The vendor's payouts still pending: those its ready earnings name, and, for one recorded before earnings named their
    // payout, those among its latest payouts.
    private async Task<List<VendorPayout>> ListPendingAsync(Vendor vendor, IEnumerable<VendorEarning> available, CancellationToken cancellationToken)
    {
        var pending = new List<VendorPayout>();

        foreach (var payoutId in available.Select(earning => earning.PayoutId).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal))
        {
            if (await _payouts.FindByIdAsync(payoutId, cancellationToken) is { Status: VendorPayoutStatus.Pending } payout)
            {
                pending.Add(payout);
            }
        }

        foreach (var payout in await _payouts.ListByVendorAsync(vendor.ItemId, PendingPayoutLookback, cancellationToken))
        {
            if (payout.Status == VendorPayoutStatus.Pending && !pending.Any(known => known.ItemId == payout.ItemId))
            {
                pending.Add(payout);
            }
        }

        return pending;
    }

    // Sends one payout's disbursement and records the answer. Returns what was paid.
    private async Task<decimal> SendAsync(
        IDisbursementService disbursements,
        Vendor vendor,
        IVendorPayoutProvider provider,
        VendorPayout payout,
        DateOnly today,
        bool resend,
        List<string> failures,
        CancellationToken cancellationToken)
    {
        VendorPayoutResult result;
        string disbursementId;

        try
        {
            (result, disbursementId) = await DisburseAsync(disbursements, provider, vendor, payout, today, resend, cancellationToken);
        }
        catch (Exception exception)
        {
            MarketplaceTelemetry.RecordPayout(provider.Name, CrestAppsOrchardCoreTelemetry.GetOutcome(exception));

            throw;
        }

        if (result.RetryLater)
        {
            // Whether a transfer exists isn't known, so nothing is marked failed and the key is kept: the payout stays
            // pending, and the next run asks its disbursement, and the provider, about it first.
            MarketplaceTelemetry.RecordPayout(provider.Name, MarketplaceTelemetry.OutcomeUnsettled);

            var pending = result.IsNotFound ? await RecordNotFoundAsync(payout, disbursementId, result.Failure, cancellationToken) : result.Failure;

            failures.Add(pending);
            _logger.LogWarning("The payout of order '{OrderNumber}' to vendor '{VendorId}' stays pending: {Failure}", payout.OrderNumber, vendor.ItemId, pending);

            return 0m;
        }

        var failure = result.Failure;

        MarketplaceTelemetry.RecordPayout(provider.Name, failure is null ? MarketplaceTelemetry.OutcomePaid : MarketplaceTelemetry.OutcomeFailed);

        // Read again: a commit since detached what was read before it, and saving a detached copy would add a row.
        var stored = await _payouts.FindByIdAsync(payout.ItemId, cancellationToken) ?? payout;

        stored.TransferId = result.TransferId;
        stored.Failure = failure;
        stored.DisbursementId = disbursementId ?? stored.DisbursementId;
        stored.Status = failure is null ? VendorPayoutStatus.Paid : VendorPayoutStatus.Failed;

        await _payouts.UpdateAsync(stored, cancellationToken);

        foreach (var earningId in payout.EarningIds ?? [])
        {
            if (await _earnings.FindByIdAsync(earningId, cancellationToken) is not { } earning)
            {
                continue;
            }

            if (failure is null)
            {
                earning.Status = VendorEarningStatus.PaidOut;
                earning.PayoutId = stored.ItemId;
            }
            else
            {
                // A disbursement that failed keeps its key, so the next try of these earnings needs a new key.
                earning.RefusedPayouts++;
                earning.PayoutId = null;
            }

            await _earnings.UpdateAsync(earning, cancellationToken);
        }

        if (failure is not null)
        {
            failures.Add(failure);
            _logger.LogWarning("Order '{OrderNumber}' couldn't be paid out to vendor '{VendorId}': {Failure}", stored.OrderNumber, vendor.ItemId, failure);

            return 0m;
        }

        return payout.Amount;
    }

    /// <summary>
    /// Groups a vendor's released earnings into transfers, one per order and currency. What the vendor owes back (a refund
    /// after an order was paid out) is a balance carried against its next orders: an order that doesn't cover what is still
    /// owed is held back, its earnings left ready, until a later one does; that one is paid what's left and settles every
    /// earning held against it, so the vendor never keeps both, however small the orders after the refund are.
    /// </summary>
    /// <param name="earnings">The vendor's released earnings.</param>
    internal static IReadOnlyList<PayoutBatch> PlanPayouts(IEnumerable<VendorEarning> earnings)
    {
        var batches = new List<PayoutBatch>();

        foreach (var currency in earnings.GroupBy(earning => earning.Currency ?? string.Empty, StringComparer.OrdinalIgnoreCase))
        {
            var orders = currency
                .GroupBy(earning => earning.OrderId)
                .Select(group => new PayoutBatch(group.Key, currency.Key, Money.Round(group.Sum(earning => earning.Net), currency.Key), [.. group]))
                .OrderBy(batch => batch.Earnings.Min(earning => earning.CreatedUtc))
                .ThenBy(batch => batch.OrderId, StringComparer.Ordinal)
                .ToList();

            var owed = orders.Where(batch => batch.Amount <= 0m).ToList();
            var deficit = Money.Round(-owed.Sum(batch => batch.Amount), currency.Key);
            var carried = owed.SelectMany(batch => batch.Earnings).ToList();

            foreach (var batch in orders.Where(batch => batch.Amount > 0m))
            {
                if (carried.Count == 0)
                {
                    batches.Add(batch);

                    continue;
                }

                if (batch.Amount <= deficit)
                {
                    // Not enough to cover what's owed: held back with the debt until an order that is.
                    deficit = Money.Round(deficit - batch.Amount, currency.Key);
                    carried.AddRange(batch.Earnings);

                    continue;
                }

                batches.Add(batch with { Amount = Money.Round(batch.Amount - deficit, currency.Key), Earnings = [.. batch.Earnings, .. carried] });
                deficit = 0m;
                carried = [];
            }
        }

        return batches;
    }

    /// <summary>
    /// One transfer a payout run makes: the order it's paid from, and every earning it settles.
    /// </summary>
    internal sealed record PayoutBatch(string OrderId, string Currency, decimal Amount, IReadOnlyList<VendorEarning> Earnings);

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

    /// <summary>
    /// Returns the payout provider named <paramref name="name"/>, or the only one enabled when no name is given;
    /// <see langword="null"/> when none matches or several are enabled and none is named.
    /// </summary>
    /// <param name="providers">The enabled payout providers.</param>
    /// <param name="name">The name chosen in the settings, if any.</param>
    public static IVendorPayoutProvider SelectPayoutProvider(IEnumerable<IVendorPayoutProvider> providers, string name)
    {
        ArgumentNullException.ThrowIfNull(providers);

        if (!string.IsNullOrWhiteSpace(name))
        {
            return providers.FirstOrDefault(provider => string.Equals(provider.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        var enabled = providers.Take(2).ToList();

        return enabled.Count == 1 ? enabled[0] : null;
    }

    // Sends the payout as a disbursement and says what became of it. The payout's key is the disbursement's, so a payout
    // sent again finds the disbursement it made: settled, it is answered with that transfer and nothing is sent. Only a
    // disbursement that failed (the provider refused it), or one that was never sent, is proof that no transfer exists;
    // anything else leaves the payout pending with its key.
    private async Task<(VendorPayoutResult Result, string DisbursementId)> DisburseAsync(
        IDisbursementService disbursements,
        IVendorPayoutProvider provider,
        Vendor vendor,
        VendorPayout payout,
        DateOnly today,
        bool resend,
        CancellationToken cancellationToken)
    {
        var manager = _payoutServices.DisbursementManager;
        var disbursement = await manager.FindByIdempotencyKeyAsync(payout.IdempotencyKey, cancellationToken);

        if (disbursement is null)
        {
            var created = await disbursements.CreateAsync(new DisbursementRequest
            {
                Payee = new PartyRef(MarketplaceConstants.VendorPartyKind, vendor.ItemId),
                PayeeName = vendor.Name,
                IsBusinessPayee = true,
                Amount = payout.Amount,
                Currency = payout.Currency,
                Method = provider.Method,
                SourceKind = MarketplaceConstants.PayoutSourceKind,
                SourceId = payout.OrderId,
                AccessScope = vendor.ItemId,
                Memo = string.IsNullOrEmpty(payout.OrderNumber) ? null : S["Order {0}", payout.OrderNumber].Value,
                EffectiveDate = today,
                IdempotencyKey = payout.IdempotencyKey,
            }, cancellationToken);

            if (created.Disbursement is null)
            {
                // Nothing was recorded, so nothing was sent.
                return created.Succeeded
                    ? (VendorPayoutResult.Unsettled(S["The payout was not recorded as a disbursement."].Value), null)
                    : (VendorPayoutResult.Refused(string.Join(" ", created.Errors)), null);
            }

            disbursement = created.Disbursement;
        }

        if (disbursement.Status == DisbursementStatus.Pending)
        {
            var (batch, problems) = await disbursements.CreateBatchAsync(provider.Name, [disbursement.ItemId], today, cancellationToken);

            if (batch is null)
            {
                // Never sent: cancelled, so no one can send it later beside the payout that replaces it.
                return await disbursements.CancelAsync(disbursement, string.Join(" ", problems), cancellationToken)
                    ? (VendorPayoutResult.Refused(string.Join(" ", problems)), disbursement.ItemId)
                    : (VendorPayoutResult.Unsettled(string.Join(" ", problems)), disbursement.ItemId);
            }

            var errors = await disbursements.ReleaseBatchAsync(batch, cancellationToken);

            disbursement = await manager.FindByIdAsync(disbursement.ItemId, cancellationToken) ?? disbursement;

            if (errors.Count > 0 && disbursement.Status == DisbursementStatus.Batched)
            {
                // The provider sent nothing (such as a charge it couldn't read) or may have sent it: the batch is sent again,
                // or checked and reopened, before the payout is.
                return (VendorPayoutResult.Unsettled(string.Join(" ", errors)), disbursement.ItemId);
            }
        }
        else if (disbursement.Status == DisbursementStatus.Batched &&
            !string.IsNullOrEmpty(disbursement.BatchId) &&
            await _payoutServices.BatchManager.FindByIdAsync(disbursement.BatchId, cancellationToken) is { Status: DisbursementBatchStatus.Open } openBatch)
        {
            // The provider sent nothing the last time (it answered that the whole batch failed): the same batch is sent again.
            var errors = await disbursements.ReleaseBatchAsync(openBatch, cancellationToken);

            disbursement = await manager.FindByIdAsync(disbursement.ItemId, cancellationToken) ?? disbursement;

            if (errors.Count > 0 && disbursement.Status == DisbursementStatus.Batched)
            {
                return (VendorPayoutResult.Unsettled(string.Join(" ", errors)), disbursement.ItemId);
            }
        }

        switch (disbursement.Status)
        {
            case DisbursementStatus.Settled when string.IsNullOrEmpty(disbursement.TraceNumber):
                return await ConfirmSettledAsync(provider, disbursement, payout, cancellationToken);

            case DisbursementStatus.Settled:
                return (VendorPayoutResult.Paid(disbursement.TraceNumber), disbursement.ItemId);

            case DisbursementStatus.Failed or DisbursementStatus.Canceled or DisbursementStatus.Returned or DisbursementStatus.Reversed:
                return (VendorPayoutResult.Refused(disbursement.FailureReason ?? S["The payout was not sent."].Value), disbursement.ItemId);

            case DisbursementStatus.Submitted when resend:
            {
                // Sent before without a known outcome: the provider says whether the transfer was made, since its key may
                // have expired there and sending again could make a second one.
                var found = await provider.FindTransferAsync(disbursement, cancellationToken);

                if (found is null)
                {
                    // Not found proves nothing: the provider may still be making it (an incident it recovers from), so the
                    // payout stays pending under its key until the transfer appears or staff confirm none was made.
                    return (VendorPayoutResult.NotFound(S["No transfer for the payout of order {0} has been found yet. It stays pending under its key, and the next payout run asks again.", payout.OrderNumber].Value), disbursement.ItemId);
                }

                if (found.RetryLater || found.Failure is not null)
                {
                    return (found with { RetryLater = true }, disbursement.ItemId);
                }

                disbursement.TraceNumber = found.TransferId ?? disbursement.TraceNumber;

                return await disbursements.MarkSettledAsync(disbursement, cancellationToken)
                    ? (VendorPayoutResult.Paid(disbursement.TraceNumber), disbursement.ItemId)
                    : (VendorPayoutResult.Unsettled(S["The payout of order {0} was made but couldn't be recorded as settled.", payout.OrderNumber].Value), disbursement.ItemId);
            }

            case DisbursementStatus.Submitted:
                return (VendorPayoutResult.Unsettled(S["The payout of order {0} was sent, and whether it went through isn't known yet. The next payout run asks again.", payout.OrderNumber].Value), disbursement.ItemId);

            default:
                return (VendorPayoutResult.Unsettled(S["The payout of order {0} is in a batch that wasn't released. It stays pending until it is.", payout.OrderNumber].Value), disbursement.ItemId);
        }
    }

    // A payout provider settles a disbursement with its transfer's ID, so one settled without it was settled by the
    // generic settle path (the "Settle Disbursement Batches" workflow or a staff click) while its outcome wasn't known.
    // The provider says whether the transfer was made: found, the payout is paid with it; not found or not answered, it
    // stays pending under its key and the next run asks again, until staff confirm no transfer was made.
    private async Task<(VendorPayoutResult Result, string DisbursementId)> ConfirmSettledAsync(
        IVendorPayoutProvider provider,
        Disbursement disbursement,
        VendorPayout payout,
        CancellationToken cancellationToken)
    {
        var found = await provider.FindTransferAsync(disbursement, cancellationToken);

        if (found is null)
        {
            return (VendorPayoutResult.NotFound(S["No transfer for the payout of order {0} has been found yet, although its disbursement was settled. It stays pending under its key, and the next payout run asks again.", payout.OrderNumber].Value), disbursement.ItemId);
        }

        if (found.RetryLater || found.Failure is not null || string.IsNullOrEmpty(found.TransferId))
        {
            return (found with { RetryLater = true, Failure = found.Failure ?? S["Whether the payout of order {0} was made isn't known yet. The next payout run asks again.", payout.OrderNumber].Value }, disbursement.ItemId);
        }

        disbursement.TraceNumber = found.TransferId;

        await _payoutServices.DisbursementManager.UpdateAsync(disbursement, cancellationToken: cancellationToken);

        return (VendorPayoutResult.Paid(found.TransferId), disbursement.ItemId);
    }

    private async Task ReleaseAsync(VendorEarning earning, DateTime now, CancellationToken cancellationToken)
    {
        earning.Status = VendorEarningStatus.Available;
        earning.ReleasedUtc = now;

        await _earnings.UpdateAsync(earning, cancellationToken);
    }
}
