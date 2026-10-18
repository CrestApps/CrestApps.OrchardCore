using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Customers.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Entities;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICouponRedemptionService"/>, keeping each use of a coupon as a
/// <see cref="CouponRedemption"/> in the coupon collection.
/// </summary>
/// <remarks>
/// Every change to what a coupon has left is made under the coupon's lock and committed before the lock is let go,
/// so a second checkout checking the coupon sees the first one's hold. What a coupon has used is its
/// <see cref="Coupon.RedemptionCount"/>, plus uses recorded while the lock couldn't be taken, plus the uses other
/// checkouts hold right now.
/// </remarks>
public sealed class CouponRedemptionService : ICouponRedemptionService
{
    private const string Collection = CheckoutConstants.CouponCollectionName;

    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(1);

    private static readonly string _held = nameof(CouponRedemptionStatus.Held);
    private static readonly string _redeemed = nameof(CouponRedemptionStatus.Redeemed);

    private readonly ICouponStore _couponStore;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CouponRedemptionService"/> class.
    /// </summary>
    public CouponRedemptionService(
        ICouponStore couponStore,
        ISession session,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IServiceProvider serviceProvider,
        IClock clock,
        ILogger<CouponRedemptionService> logger)
    {
        _couponStore = couponStore;
        _session = session;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _serviceProvider = serviceProvider;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<CouponAvailability> GetAvailabilityAsync(Coupon coupon, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(coupon);

        var customer = await ResolveCustomerAsync(flow, cancellationToken);

        return await CheckAsync(coupon, flow?.Session?.SessionId, customer, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<CouponAvailability> HoldAsync(CheckoutFlow flow, DiscountLine discount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(discount);

        var sessionId = flow.Session.SessionId;
        var (locker, locked) = await LockAsync(discount.Code);

        if (!locked)
        {
            return CouponAvailability.Busy;
        }

        await using (locker)
        {
            var coupon = await _couponStore.GetByCodeAsync(discount.Code, cancellationToken);
            var customer = await ResolveCustomerAsync(flow, cancellationToken);
            var availability = coupon is null
                ? CouponAvailability.Unavailable
                : await CheckAsync(coupon, sessionId, customer, cancellationToken);

            if (availability != CouponAvailability.Available)
            {
                return availability;
            }

            var settings = await _siteService.GetSettingsAsync<CheckoutSettings>();
            var now = _clock.UtcNow;

            // A checkout that begins its payment again keeps the use it holds, held for longer.
            var hold = await FindAsync(coupon.Code, sessionId, CouponRedemptionStatus.Held, cancellationToken)
                ?? New(coupon, flow, now);

            hold.Amount = discount.Amount;
            hold.Currency = (flow.Session as CheckoutSession)?.Currency ?? hold.Currency;
            hold.CustomerId = customer.Id;
            hold.CustomerEmail = customer.Email;
            hold.ExpiresUtc = now.AddHours(Math.Max(1, settings.SessionLifetimeHours));

            await _session.SaveAsync(hold, collection: Collection, cancellationToken: cancellationToken);

            // Committed while the lock is held, so the next checkout to check the coupon counts this hold.
            await _session.SaveChangesAsync(cancellationToken);

            return CouponAvailability.Available;
        }
    }

    /// <inheritdoc/>
    public async Task RedeemAsync(CheckoutFlow flow, DiscountLine discount, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(discount);

        if (string.IsNullOrEmpty(discount.Code))
        {
            return;
        }

        var sessionId = flow.Session.SessionId;
        var (locker, locked) = await LockAsync(discount.Code);

        if (!locked)
        {
            // The customer has paid, so the use is recorded anyway; the next use that can lock the coupon counts it.
            var unlocked = await FindAsync(discount.Code, sessionId, CouponRedemptionStatus.Held, cancellationToken)
                ?? await NewAsync(discount, flow, cancellationToken);

            await MarkRedeemedAsync(unlocked, discount, counted: false, cancellationToken);

            _logger.LogWarning("Could not lock coupon '{Code}' to redeem it for checkout '{SessionId}'. The use is recorded and will be counted by the next one.", discount.Code, sessionId);

            return;
        }

        await using (locker)
        {
            if (await FindAsync(discount.Code, sessionId, CouponRedemptionStatus.Redeemed, cancellationToken) is not null)
            {
                return;
            }

            var coupon = await _couponStore.GetByCodeAsync(discount.Code, cancellationToken);
            var redemption = await FindAsync(discount.Code, sessionId, CouponRedemptionStatus.Held, cancellationToken)
                ?? await NewAsync(discount, flow, cancellationToken);

            await MarkRedeemedAsync(redemption, discount, counted: coupon is not null, cancellationToken);

            if (coupon is not null)
            {
                var uncounted = await CountUncountedAsync(coupon, redemption.ItemId, cancellationToken);

                coupon.RedemptionCount += 1 + uncounted;

                if (coupon.MaxRedemptions.HasValue && coupon.RedemptionCount > coupon.MaxRedemptions.Value)
                {
                    _logger.LogWarning("Coupon '{Code}' has been used {Count} times, more than its limit of {Limit}: a hold lapsed before its checkout completed.", coupon.Code, coupon.RedemptionCount, coupon.MaxRedemptions.Value);
                }

                await _couponStore.UpdateAsync(coupon, cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var hold in await _session
            .Query<CouponRedemption, CouponRedemptionIndex>(index => index.SessionId == sessionId && index.Status == _held, collection: Collection)
            .ListAsync(cancellationToken))
        {
            hold.Status = CouponRedemptionStatus.Released;
            hold.EndedUtc = now;

            await _session.SaveAsync(hold, collection: Collection, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<int> ReturnAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return 0;
        }

        await ReleaseAsync(sessionId, cancellationToken);

        var redeemed = await _session
            .Query<CouponRedemption, CouponRedemptionIndex>(index => index.SessionId == sessionId && index.Status == _redeemed, collection: Collection)
            .ListAsync(cancellationToken);

        var returned = 0;

        foreach (var redemption in redeemed)
        {
            var (locker, locked) = await LockAsync(redemption.Code);

            if (!locked)
            {
                _logger.LogWarning("Could not lock coupon '{Code}' to give back the use of checkout '{SessionId}'.", redemption.Code, sessionId);

                continue;
            }

            await using (locker)
            {
                var coupon = await _couponStore.GetByCodeAsync(redemption.Code, cancellationToken);

                if (coupon is not null && redemption.IsCounted)
                {
                    coupon.RedemptionCount = Math.Max(0, coupon.RedemptionCount - 1);

                    await _couponStore.UpdateAsync(coupon, cancellationToken);
                }

                redemption.Status = CouponRedemptionStatus.Returned;
                redemption.EndedUtc = _clock.UtcNow;

                await _session.SaveAsync(redemption, collection: Collection, cancellationToken: cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);

                returned++;
            }
        }

        return returned;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CouponRedemption>> ListAsync(string code, int take, CancellationToken cancellationToken = default)
    {
        var normalized = CouponRedemptionIndexProvider.Normalize(code);

        if (normalized is null)
        {
            return [];
        }

        var redemptions = await _session
            .Query<CouponRedemption, CouponRedemptionIndex>(index => index.NormalizedCode == normalized, collection: Collection)
            .OrderByDescending(index => index.CreatedUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ListAsync(cancellationToken);

        return [.. redemptions];
    }

    /// <inheritdoc/>
    public Task<int> CountHeldAsync(string code, CancellationToken cancellationToken = default)
    {
        var normalized = CouponRedemptionIndexProvider.Normalize(code);

        if (normalized is null)
        {
            return Task.FromResult(0);
        }

        var now = _clock.UtcNow;

        return _session
            .QueryIndex<CouponRedemptionIndex>(index => index.NormalizedCode == normalized && index.Status == _held && index.ExpiresUtc > now, collection: Collection)
            .CountAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<int> ForgetCustomersAsync(IEnumerable<string> sessionIds, CancellationToken cancellationToken = default)
    {
        var changed = 0;

        foreach (var batch in (sessionIds ?? []).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal).Chunk(100))
        {
            var redemptions = await _session
                .Query<CouponRedemption, CouponRedemptionIndex>(index => index.SessionId.IsIn(batch), collection: Collection)
                .ListAsync(cancellationToken);

            foreach (var redemption in redemptions)
            {
                if (redemption.CustomerId is null && redemption.CustomerEmail is null)
                {
                    continue;
                }

                redemption.CustomerId = null;
                redemption.CustomerEmail = null;

                await _session.SaveAsync(redemption, collection: Collection, cancellationToken: cancellationToken);

                changed++;
            }
        }

        return changed;
    }

    private async Task<CouponAvailability> CheckAsync(Coupon coupon, string sessionId, CouponCustomer customer, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        if (!coupon.IsRedeemable(now))
        {
            return coupon.IsEnabled &&
                (!coupon.StartsUtc.HasValue || now >= coupon.StartsUtc.Value) &&
                (!coupon.EndsUtc.HasValue || now <= coupon.EndsUtc.Value)
                ? CouponAvailability.UsedUp
                : CouponAvailability.Unavailable;
        }

        var normalized = CouponRedemptionIndexProvider.Normalize(coupon.Code);
        sessionId ??= string.Empty;

        if (coupon.MaxRedemptions.HasValue)
        {
            var uncounted = await CountUncountedAsync(coupon, exceptItemId: null, cancellationToken);
            var heldByOthers = await _session
                .QueryIndex<CouponRedemptionIndex>(index => index.NormalizedCode == normalized && index.Status == _held && index.ExpiresUtc > now && index.SessionId != sessionId, collection: Collection)
                .CountAsync(cancellationToken);

            if (coupon.RedemptionCount + uncounted + heldByOthers >= coupon.MaxRedemptions.Value)
            {
                return CouponAvailability.UsedUp;
            }
        }

        if (coupon.MaxRedemptionsPerCustomer.HasValue && customer.IsKnown)
        {
            var used = await CountForCustomerAsync(normalized, sessionId, customer, now, cancellationToken);

            if (used >= coupon.MaxRedemptionsPerCustomer.Value)
            {
                return CouponAvailability.CustomerLimitReached;
            }
        }

        return CouponAvailability.Available;
    }

    // A customer is their account or their email, so a buyer who signs in on one order and checks out as a guest
    // with the same email on the next is still one customer.
    private Task<int> CountForCustomerAsync(string code, string sessionId, CouponCustomer customer, DateTime now, CancellationToken cancellationToken)
    {
        var id = customer.Id;
        var email = CouponRedemptionIndexProvider.Normalize(customer.Email);

        IQueryIndex<CouponRedemptionIndex> query;

        if (id is not null && email is not null)
        {
            query = _session.QueryIndex<CouponRedemptionIndex>(index =>
                index.NormalizedCode == code &&
                (index.CustomerId == id || index.NormalizedEmail == email) &&
                (index.Status == _redeemed || (index.Status == _held && index.ExpiresUtc > now && index.SessionId != sessionId)), collection: Collection);
        }
        else if (id is not null)
        {
            query = _session.QueryIndex<CouponRedemptionIndex>(index =>
                index.NormalizedCode == code &&
                index.CustomerId == id &&
                (index.Status == _redeemed || (index.Status == _held && index.ExpiresUtc > now && index.SessionId != sessionId)), collection: Collection);
        }
        else
        {
            query = _session.QueryIndex<CouponRedemptionIndex>(index =>
                index.NormalizedCode == code &&
                index.NormalizedEmail == email &&
                (index.Status == _redeemed || (index.Status == _held && index.ExpiresUtc > now && index.SessionId != sessionId)), collection: Collection);
        }

        return query.CountAsync(cancellationToken);
    }

    // Folds the uses recorded without the lock into the count: they are marked counted here, and the caller adds
    // how many there were.
    private async Task<int> CountUncountedAsync(Coupon coupon, string exceptItemId, CancellationToken cancellationToken)
    {
        var normalized = CouponRedemptionIndexProvider.Normalize(coupon.Code);
        var uncounted = await _session
            .Query<CouponRedemption, CouponRedemptionIndex>(index => index.NormalizedCode == normalized && index.Status == _redeemed && !index.IsCounted, collection: Collection)
            .ListAsync(cancellationToken);

        var count = 0;

        foreach (var redemption in uncounted)
        {
            if (string.Equals(redemption.ItemId, exceptItemId, StringComparison.Ordinal))
            {
                continue;
            }

            count++;

            if (exceptItemId is not null)
            {
                redemption.IsCounted = true;

                await _session.SaveAsync(redemption, collection: Collection, cancellationToken: cancellationToken);
            }
        }

        return count;
    }

    private async Task MarkRedeemedAsync(CouponRedemption redemption, DiscountLine discount, bool counted, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        redemption.Status = CouponRedemptionStatus.Redeemed;
        redemption.IsCounted = counted;
        redemption.Amount = discount.Amount;
        redemption.RedeemedUtc = now;
        redemption.ExpiresUtc = null;

        await _session.SaveAsync(redemption, collection: Collection, cancellationToken: cancellationToken);
    }

    private Task<CouponRedemption> FindAsync(string code, string sessionId, CouponRedemptionStatus status, CancellationToken cancellationToken)
    {
        var normalized = CouponRedemptionIndexProvider.Normalize(code);
        var name = status.ToString();

        return _session
            .Query<CouponRedemption, CouponRedemptionIndex>(index => index.NormalizedCode == normalized && index.SessionId == sessionId && index.Status == name, collection: Collection)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<CouponRedemption> NewAsync(DiscountLine discount, CheckoutFlow flow, CancellationToken cancellationToken)
    {
        var customer = await ResolveCustomerAsync(flow, cancellationToken);
        var redemption = New(new Coupon { Code = discount.Code }, flow, _clock.UtcNow);

        redemption.CustomerId = customer.Id;
        redemption.CustomerEmail = customer.Email;

        return redemption;
    }

    private static CouponRedemption New(Coupon coupon, CheckoutFlow flow, DateTime now)
    {
        var session = flow.Session as CheckoutSession;

        return new CouponRedemption
        {
            ItemId = IdGenerator.GenerateId(),
            Code = coupon.Code,
            SessionId = flow.Session.SessionId,
            ReferenceType = session?.ReferenceType,
            ReferenceId = session?.ReferenceId,
            Currency = session?.Currency,
            Status = CouponRedemptionStatus.Held,
            CreatedUtc = now,
        };
    }

    private async Task<CouponCustomer> ResolveCustomerAsync(CheckoutFlow flow, CancellationToken cancellationToken)
    {
        if (flow?.Session is not CheckoutSession session)
        {
            return default;
        }

        var scope = session.TryGet<CheckoutDiscountScope>(out var stored) ? stored : null;
        var id = !string.IsNullOrEmpty(session.OwnerId) ? session.OwnerId : scope?.CustomerId;

        var email = scope?.CustomerEmail;

        if (string.IsNullOrWhiteSpace(email) && session.TryGet<CheckoutContactInfo>(out var contact))
        {
            email = contact.Email;
        }

        // The account's email is read only when the checkout holds none, since it is a lookup.
        if (string.IsNullOrWhiteSpace(email) &&
            !string.IsNullOrEmpty(session.OwnerId) &&
            _serviceProvider.GetService<ICustomerContactResolver>() is { } contactResolver)
        {
            var account = await contactResolver.ResolveAsync(CustomerOwner.ForUser(session.OwnerId), guestContact: null, cancellationToken);

            email = account?.Email;
        }

        return new CouponCustomer(string.IsNullOrWhiteSpace(id) ? null : id, string.IsNullOrWhiteSpace(email) ? null : email.Trim());
    }

    private Task<(ILocker Locker, bool Locked)> LockAsync(string code)
        => _distributedLock.TryAcquireLockAsync("COUPON_" + code.Trim().ToUpperInvariant(), _lockTimeout, _lockExpiration);

    private readonly record struct CouponCustomer(string Id, string Email)
    {
        public bool IsKnown
            => Id is not null || Email is not null;
    }
}
