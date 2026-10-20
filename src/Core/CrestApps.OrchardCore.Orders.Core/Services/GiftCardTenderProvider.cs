using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// Lets gift cards pay toward checkouts. The checkout's tender ledger holds what a card pays while the buyer pays the
/// rest, and calls this to take it off the card, or put it back for a refund, under the card's lock.
/// </summary>
public sealed class GiftCardTenderProvider : ICheckoutTenderProvider
{
    private readonly IGiftCardStore _store;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public GiftCardTenderProvider(
        IGiftCardStore store,
        IClock clock,
        IStringLocalizer<GiftCardTenderProvider> stringLocalizer)
    {
        _store = store;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public string Key
        => OrderConstants.GiftCardTenderKey;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TenderLine>> GetTendersAsync(CheckoutTenderContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var lines = new List<TenderLine>();
        var now = _clock.UtcNow;

        foreach (var card in await _store.GetAsync(context.References, cancellationToken))
        {
            // A card that ran out, was disabled or expired since it was applied simply stops paying; the buyer pays
            // the rest by card rather than being stopped.
            if (card.CanPay(now, context.Invoice.Currency))
            {
                lines.Add(new TenderLine
                {
                    Reference = card.ItemId,
                    Description = Describe(card),
                    Amount = card.Balance,
                });
            }
        }

        return lines;
    }

    /// <inheritdoc/>
    public async Task<decimal?> GetBalanceAsync(string reference, string currency, CancellationToken cancellationToken = default)
    {
        var card = await _store.FindByIdAsync(reference, cancellationToken);

        return card is not null && card.CanPay(_clock.UtcNow, currency) ? card.Balance : null;
    }

    /// <inheritdoc/>
    public async Task DebitAsync(string reference, decimal amount, string currency, CheckoutFlow flow, CancellationToken cancellationToken = default)
    {
        var card = await _store.FindByIdAsync(reference, cancellationToken)
            ?? throw new InvalidOperationException($"The gift card '{reference}' no longer exists.");

        // The hold kept the money for this checkout, so it is taken even if the card was disabled since.
        card.ChangeBalance(-amount, S["Spent at checkout"], _clock.UtcNow, flow?.Session?.SessionId);

        await _store.UpdateAsync(card, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task CreditAsync(string reference, decimal amount, string currency, string reason, CancellationToken cancellationToken = default)
    {
        var card = await _store.FindByIdAsync(reference, cancellationToken);

        if (card is null)
        {
            return;
        }

        card.ChangeBalance(amount, string.IsNullOrWhiteSpace(reason) ? S["Refunded"] : reason, _clock.UtcNow);

        await _store.UpdateAsync(card, cancellationToken);
    }

    /// <summary>
    /// Returns what a buyer is shown for the card: its last four characters, never the whole code.
    /// </summary>
    public string Describe(GiftCard card)
        => S["Gift card ending {0}", card.LastCharacters];
}
