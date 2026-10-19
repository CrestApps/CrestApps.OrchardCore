using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Payments;
using OrchardCore;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Issues gift cards and changes their balances by hand.
/// </summary>
public sealed class GiftCardService
{
    private readonly IGiftCardStore _store;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    public GiftCardService(
        IGiftCardStore store,
        ISession session,
        IDistributedLock distributedLock,
        IClock clock)
    {
        _store = store;
        _session = session;
        _distributedLock = distributedLock;
        _clock = clock;
    }

    /// <summary>
    /// Makes a gift card with <paramref name="value"/> on it, with a new code unless <paramref name="code"/> is given.
    /// Returns <see langword="null"/> when the given code is taken.
    /// </summary>
    public async Task<GiftCard> IssueAsync(decimal value, string currency, string reason, Action<GiftCard> configure = null, string code = null, string actor = null, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            code = GiftCardCodes.Format(code);

            if (await _store.GetByCodeAsync(code, cancellationToken) is not null)
            {
                return null;
            }
        }
        else
        {
            // A clash among 32^16 codes is as good as impossible, but checking costs one lookup.
            do
            {
                code = GiftCardCodes.Generate();
            }
            while (await _store.GetByCodeAsync(code, cancellationToken) is not null);
        }

        currency = currency?.Trim().ToUpperInvariant();
        value = Money.Round(value, currency);

        var card = new GiftCard
        {
            ItemId = IdGenerator.GenerateId(),
            Code = code,
            Currency = currency,
            InitialValue = value,
        };

        configure?.Invoke(card);
        card.ChangeBalance(value, reason, _clock.UtcNow, actor: actor);

        await _store.CreateAsync(card, cancellationToken);

        return card;
    }

    /// <summary>
    /// Adds <paramref name="amount"/> to a card's balance, or takes it off when it's negative, under the lock the
    /// checkout takes, so a checkout spending the card at the same time can't write over it. Returns the card, or
    /// <see langword="null"/> when its lock couldn't be taken.
    /// </summary>
    public async Task<GiftCard> AdjustAsync(string itemId, decimal amount, string reason, string actor, CancellationToken cancellationToken = default)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(CheckoutTenderLocks.GetKey(OrderConstants.GiftCardTenderKey, itemId), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30));

        if (!locked)
        {
            return null;
        }

        await using (locker)
        {
            var card = await _store.FindByIdAsync(itemId, cancellationToken);

            if (card is null)
            {
                return null;
            }

            card.ChangeBalance(amount, reason, _clock.UtcNow, actor: actor);

            await _store.UpdateAsync(card, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return card;
        }
    }
}
