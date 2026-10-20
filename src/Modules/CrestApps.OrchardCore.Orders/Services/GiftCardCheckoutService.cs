using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Entities;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Puts gift cards on a checkout and takes them off. The Payment step's box and the commerce API both go through
/// here.
/// </summary>
public sealed class GiftCardCheckoutService
{
    private readonly IGiftCardStore _store;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public GiftCardCheckoutService(
        IGiftCardStore store,
        IPaymentAttemptLimiter limiter,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IStringLocalizer<GiftCardCheckoutService> stringLocalizer)
    {
        _store = store;
        _limiter = limiter;
        _invoiceBuilder = invoiceBuilder;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Returns whether the checkout takes gift cards: not one that settles an existing balance, nor one that saves a card
    /// to charge later.
    /// </summary>
    public static bool AcceptsGiftCards(CheckoutFlow flow)
        => flow?.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false };

    /// <summary>
    /// Puts the card with <paramref name="code"/> on the checkout and rebuilds the invoice. Returns why it wasn't, or
    /// <see langword="null"/> when it pays toward the order.
    /// </summary>
    public async Task<string> ApplyAsync(CheckoutFlow flow, string code, string clientAddress)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (string.IsNullOrWhiteSpace(code))
        {
            return S["Enter a gift card code."];
        }

        if (!AcceptsGiftCards(flow))
        {
            return S["This payment takes no gift cards."];
        }

        // A gift card code is money, so how fast codes can be tried from one address is limited.
        if (!await _limiter.AcquireAsync("gift-card-code", clientAddress ?? "unknown"))
        {
            return S["Too many codes were tried. Wait a few minutes, then try again."];
        }

        var card = await _store.GetByCodeAsync(code);
        var currency = flow.Session.TryGet<CheckoutInvoice>(out var invoice) ? invoice.Currency : (flow.Session as CheckoutSession)?.Currency;

        if (card is null)
        {
            return S["That gift card code is not recognized."];
        }

        if (!card.IsEnabled)
        {
            return S["That gift card can't be used."];
        }

        if (card.ExpiresUtc.HasValue && card.ExpiresUtc.Value <= _clock.UtcNow)
        {
            return S["That gift card has expired."];
        }

        if (card.Balance <= 0m)
        {
            return S["That gift card has nothing left on it."];
        }

        if (!string.IsNullOrEmpty(currency) && !string.Equals(card.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            return S["That gift card is in {0}, and this order is in {1}.", card.Currency, currency];
        }

        var selection = flow.Session.TryGet<CheckoutTenderSelection>(out var existing) ? existing : new CheckoutTenderSelection();

        selection.Add(OrderConstants.GiftCardTenderKey, card.ItemId);
        flow.Session.Put(selection);

        // Rebuilt now, so the buyer sees what the card pays and what's left before they pay.
        await _invoiceBuilder.BuildAsync(flow);

        if (flow.Session.TryGet<CheckoutInvoice>(out var rebuilt) &&
            !(rebuilt.Tenders ?? []).Any(tender => tender.ProviderKey == OrderConstants.GiftCardTenderKey && tender.Reference == card.ItemId))
        {
            return S["That gift card is being used at another checkout right now, or there's nothing left to pay."];
        }

        return null;
    }

    /// <summary>
    /// Takes a gift card off the checkout and rebuilds the invoice. Returns whether it was on it.
    /// </summary>
    public async Task<bool> RemoveAsync(CheckoutFlow flow, string giftCardId)
    {
        ArgumentNullException.ThrowIfNull(flow);

        if (!flow.Session.TryGet<CheckoutTenderSelection>(out var selection) || !selection.Remove(OrderConstants.GiftCardTenderKey, giftCardId))
        {
            return false;
        }

        flow.Session.Put(selection);
        await _invoiceBuilder.BuildAsync(flow);

        return true;
    }
}
