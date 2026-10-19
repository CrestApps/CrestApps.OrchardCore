using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Modules;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// The Gift card box on the Payment step: the buyer enters a card's code, and it pays toward the order after tax.
/// </summary>
internal sealed class GiftCardCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly IGiftCardStore _store;
    private readonly IPaymentAttemptLimiter _limiter;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public GiftCardCheckoutFlowDisplayDriver(
        IGiftCardStore store,
        IPaymentAttemptLimiter limiter,
        IHttpContextAccessor httpContextAccessor,
        CheckoutInvoiceBuilder invoiceBuilder,
        IClock clock,
        IStringLocalizer<GiftCardCheckoutFlowDisplayDriver> stringLocalizer)
    {
        _store = store;
        _limiter = limiter;
        _httpContextAccessor = httpContextAccessor;
        _invoiceBuilder = invoiceBuilder;
        _clock = clock;
        S = stringLocalizer;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        if (!AcceptsGiftCards(flow) || !flow.Session.TryGet<CheckoutInvoice>(out var invoice))
        {
            return null;
        }

        var tenders = (invoice.Tenders ?? []).Where(tender => tender.ProviderKey == OrderConstants.GiftCardTenderKey).ToList();

        // A checkout with nothing left to pay and no card on it has no use for the box.
        if (tenders.Count == 0 && invoice.InitialPaymentAmount is not > 0m)
        {
            return null;
        }

        var cards = await _store.GetAsync(tenders.Select(tender => tender.Reference));

        return Initialize<CheckoutGiftCardViewModel>("CheckoutGiftCard", model =>
        {
            model.Currency = invoice.Currency;
            model.Applied = [.. tenders.Select(tender =>
            {
                var card = cards.FirstOrDefault(entry => entry.ItemId == tender.Reference);

                return new AppliedGiftCardViewModel
                {
                    ItemId = tender.Reference,
                    LastCharacters = card?.LastCharacters,
                    Amount = tender.Amount,
                    Remaining = Math.Max(0m, (card?.Balance ?? 0m) - tender.Amount),
                };
            })];
        }).Location("Aside:6");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        if (!AcceptsGiftCards(flow))
        {
            return null;
        }

        var model = new CheckoutGiftCardViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var selection = flow.Session.TryGet<CheckoutTenderSelection>(out var existing) ? existing : new CheckoutTenderSelection();
        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + OrderConstants.GiftCardTenderKey + ":";

        if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            if (selection.Remove(OrderConstants.GiftCardTenderKey, action[removePrefix.Length..]))
            {
                flow.Session.Put(selection);
                await _invoiceBuilder.BuildAsync(flow);
            }

            return await EditStepAsync(flow, context);
        }

        if (string.IsNullOrWhiteSpace(model.GiftCardCode))
        {
            return await EditStepAsync(flow, context);
        }

        // A gift card code is money, so how fast codes can be tried from one address is limited.
        var address = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        if (!await _limiter.AcquireAsync("gift-card-code", address))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["Too many codes were tried. Wait a few minutes, then try again."]);

            return await EditStepAsync(flow, context);
        }

        var card = await _store.GetByCodeAsync(model.GiftCardCode);
        var invoice = flow.Session.TryGet<CheckoutInvoice>(out var built) ? built : null;
        var currency = invoice?.Currency ?? (flow.Session as CheckoutSession)?.Currency;

        if (card is null)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card code is not recognized."]);
        }
        else if (!card.IsEnabled)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card can't be used."]);
        }
        else if (card.ExpiresUtc.HasValue && card.ExpiresUtc.Value <= _clock.UtcNow)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card has expired."]);
        }
        else if (card.Balance <= 0m)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card has nothing left on it."]);
        }
        else if (!string.IsNullOrEmpty(currency) && !string.Equals(card.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card is in {0}, and this order is in {1}.", card.Currency, currency]);
        }
        else
        {
            selection.Add(OrderConstants.GiftCardTenderKey, card.ItemId);
            flow.Session.Put(selection);

            // Rebuilt now, so the buyer sees what the card pays and what's left before they pay.
            await _invoiceBuilder.BuildAsync(flow);

            if (flow.Session.TryGet<CheckoutInvoice>(out var rebuilt) &&
                !(rebuilt.Tenders ?? []).Any(tender => tender.ProviderKey == OrderConstants.GiftCardTenderKey && tender.Reference == card.ItemId))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), S["That gift card is being used at another checkout right now, or there's nothing left to pay."]);
            }
        }

        return await EditStepAsync(flow, context);
    }

    // A checkout that settles an existing balance, or saves a card to charge later, is paid by card alone.
    private static bool AcceptsGiftCards(CheckoutFlow flow)
        => flow.Session is CheckoutSession { SettlesExistingBalance: false, SavePaymentMethod: false };
}
