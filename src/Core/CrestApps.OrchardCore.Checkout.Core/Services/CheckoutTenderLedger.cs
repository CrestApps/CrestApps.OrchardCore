using CrestApps.OrchardCore.Checkout.Core.Indexes;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments;
using Microsoft.Extensions.Logging;
using OrchardCore;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Checkout.Core.Services;

/// <summary>
/// The default <see cref="ICheckoutTenderLedger"/>: one <see cref="CheckoutTenderHold"/> per checkout and balance,
/// changed only under the balance's lock and committed before the lock is let go, so the second of two checkouts
/// spending the same money sees what the first holds.
/// </summary>
public sealed class CheckoutTenderLedger : ICheckoutTenderLedger
{
    private static readonly string _held = nameof(CheckoutTenderHoldStatus.Held);
    private static readonly string _redeemed = nameof(CheckoutTenderHoldStatus.Redeemed);

    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IEnumerable<ICheckoutTenderProvider> _providers;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    public CheckoutTenderLedger(
        ISession session,
        IDistributedLock distributedLock,
        IEnumerable<ICheckoutTenderProvider> providers,
        IClock clock,
        ILogger<CheckoutTenderLedger> logger)
    {
        _session = session;
        _distributedLock = distributedLock;
        _providers = providers;
        _clock = clock;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<decimal> GetHeldByOthersAsync(string providerKey, string reference, string sessionId, CancellationToken cancellationToken = default)
    {
        sessionId ??= string.Empty;

        var holds = await _session
            .Query<CheckoutTenderHold, CheckoutTenderHoldIndex>(index => index.ProviderKey == providerKey && index.Reference == reference && index.Status == _held && index.SessionId != sessionId)
            .ListAsync(cancellationToken);

        return holds.Sum(hold => hold.Amount);
    }

    /// <inheritdoc/>
    public async Task<TenderAvailability> HoldAsync(CheckoutFlow flow, TenderLine tender, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);
        ArgumentNullException.ThrowIfNull(tender);

        var provider = Provider(tender.ProviderKey);

        if (provider is null)
        {
            return TenderAvailability.Unavailable;
        }

        var sessionId = flow.Session.SessionId;
        var currency = (flow.Session as CheckoutSession)?.Currency;
        var (locker, locked) = await LockAsync(tender.ProviderKey, tender.Reference);

        if (!locked)
        {
            return TenderAvailability.Busy;
        }

        await using (locker)
        {
            var balance = await provider.GetBalanceAsync(tender.Reference, currency, cancellationToken);

            if (balance is null)
            {
                return TenderAvailability.Unavailable;
            }

            var available = balance.Value - await GetHeldByOthersAsync(tender.ProviderKey, tender.Reference, sessionId, cancellationToken);

            if (available < tender.Amount)
            {
                return TenderAvailability.Insufficient;
            }

            var hold = await FindAsync(sessionId, tender.ProviderKey, tender.Reference, _held, cancellationToken) ?? new CheckoutTenderHold
            {
                ItemId = IdGenerator.GenerateId(),
                SessionId = sessionId,
                ProviderKey = tender.ProviderKey,
                Reference = tender.Reference,
                Status = CheckoutTenderHoldStatus.Held,
                CreatedUtc = _clock.UtcNow,
            };

            hold.Amount = tender.Amount;
            hold.Currency = currency;
            hold.Description = tender.Description;

            await _session.SaveAsync(hold, cancellationToken: cancellationToken);

            // Committed while the lock is held, so the next checkout to look at this balance counts this hold.
            await _session.SaveChangesAsync(cancellationToken);

            return TenderAvailability.Available;
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseOthersAsync(string sessionId, IEnumerable<TenderLine> keep, CancellationToken cancellationToken = default)
    {
        var kept = (keep ?? []).Select(tender => (tender.ProviderKey, tender.Reference)).ToHashSet();

        foreach (var hold in await ListAsync(sessionId, _held, cancellationToken))
        {
            if (!kept.Contains((hold.ProviderKey, hold.Reference)))
            {
                hold.Status = CheckoutTenderHoldStatus.Released;
                await _session.SaveAsync(hold, cancellationToken: cancellationToken);
            }
        }
    }

    /// <inheritdoc/>
    public async Task RedeemAsync(CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(flow);

        foreach (var held in await ListAsync(flow.Session.SessionId, _held, cancellationToken))
        {
            var provider = Provider(held.ProviderKey);

            if (provider is null)
            {
                continue;
            }

            var (locker, locked) = await LockAsync(held.ProviderKey, held.Reference);

            // The checkout is completing, so the money is taken even without the lock: the hold kept it from being
            // spent elsewhere.
            await using (locker)
            {
                var hold = await FindAsync(held.SessionId, held.ProviderKey, held.Reference, _held, cancellationToken);

                if (hold is null)
                {
                    continue;
                }

                await provider.DebitAsync(hold.Reference, hold.Amount, hold.Currency, flow, cancellationToken);

                hold.Status = CheckoutTenderHoldStatus.Redeemed;
                hold.RedeemedUtc = _clock.UtcNow;

                await _session.SaveAsync(hold, cancellationToken: cancellationToken);

                if (!locked)
                {
                    _logger.LogWarning("Took {Amount} off balance '{Reference}' of '{ProviderKey}' for checkout '{SessionId}' without its lock.", hold.Amount, hold.Reference, hold.ProviderKey, hold.SessionId);
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task ReleaseAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        foreach (var hold in await ListAsync(sessionId, _held, cancellationToken))
        {
            hold.Status = CheckoutTenderHoldStatus.Released;
            await _session.SaveAsync(hold, cancellationToken: cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<CheckoutTenderHold>> GetRedeemedAsync(string sessionId, CancellationToken cancellationToken = default)
        => [.. (await ListAsync(sessionId, _redeemed, cancellationToken)).Where(hold => hold.Amount > hold.ReturnedAmount)];

    /// <inheritdoc/>
    public async Task<decimal> ReturnAsync(string sessionId, decimal amount, string reason, CancellationToken cancellationToken = default)
    {
        var returned = 0m;

        foreach (var redeemed in (await GetRedeemedAsync(sessionId, cancellationToken)).OrderByDescending(hold => hold.RedeemedUtc))
        {
            if (amount - returned <= 0m || Provider(redeemed.ProviderKey) is not { } provider)
            {
                continue;
            }

            var (locker, locked) = await LockAsync(redeemed.ProviderKey, redeemed.Reference);

            if (!locked)
            {
                _logger.LogWarning("Could not lock balance '{Reference}' of '{ProviderKey}' to put back what checkout '{SessionId}' took.", redeemed.Reference, redeemed.ProviderKey, sessionId);

                continue;
            }

            await using (locker)
            {
                var hold = await FindAsync(redeemed.SessionId, redeemed.ProviderKey, redeemed.Reference, _redeemed, cancellationToken);
                var portion = hold is null ? 0m : Money.Round(Math.Min(amount - returned, hold.Amount - hold.ReturnedAmount), hold.Currency);

                if (portion <= 0m)
                {
                    continue;
                }

                await provider.CreditAsync(hold.Reference, portion, hold.Currency, reason, cancellationToken);

                hold.ReturnedAmount += portion;
                returned += portion;

                await _session.SaveAsync(hold, cancellationToken: cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);
            }
        }

        return returned;
    }

    private ICheckoutTenderProvider Provider(string key)
        => _providers.FirstOrDefault(provider => string.Equals(provider.Key, key, StringComparison.Ordinal));

    private Task<(ILocker Locker, bool Locked)> LockAsync(string providerKey, string reference)
        => _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(providerKey, reference), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

    private Task<CheckoutTenderHold> FindAsync(string sessionId, string providerKey, string reference, string status, CancellationToken cancellationToken)
        => _session
            .Query<CheckoutTenderHold, CheckoutTenderHoldIndex>(index => index.SessionId == sessionId && index.ProviderKey == providerKey && index.Reference == reference && index.Status == status)
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<IReadOnlyList<CheckoutTenderHold>> ListAsync(string sessionId, string status, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return [];
        }

        return [.. await _session
            .Query<CheckoutTenderHold, CheckoutTenderHoldIndex>(index => index.SessionId == sessionId && index.Status == status)
            .ListAsync(cancellationToken)];
    }
}
