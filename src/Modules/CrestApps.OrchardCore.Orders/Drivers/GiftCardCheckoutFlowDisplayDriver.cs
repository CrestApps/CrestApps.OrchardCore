using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
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
    private readonly GiftCardCheckoutService _giftCards;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GiftCardCheckoutFlowDisplayDriver(
        IGiftCardStore store,
        GiftCardCheckoutService giftCards,
        IHttpContextAccessor httpContextAccessor)
    {
        _store = store;
        _giftCards = giftCards;
        _httpContextAccessor = httpContextAccessor;
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

        var action = _httpContextAccessor.HttpContext?.Request.HasFormContentType == true
            ? _httpContextAccessor.HttpContext.Request.Form[CheckoutConstants.FormActions.FieldName].ToString()
            : null;
        var removePrefix = CheckoutConstants.FormActions.RemoveTenderPrefix + OrderConstants.GiftCardTenderKey + ":";

        if (action?.StartsWith(removePrefix, StringComparison.Ordinal) == true)
        {
            await _giftCards.RemoveAsync(flow, action[removePrefix.Length..]);

            return await EditStepAsync(flow, context);
        }

        if (string.IsNullOrWhiteSpace(model.GiftCardCode))
        {
            return await EditStepAsync(flow, context);
        }

        var address = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown";

        if (await _giftCards.ApplyAsync(flow, model.GiftCardCode, address) is { } error)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.GiftCardCode), error);
        }

        return await EditStepAsync(flow, context);
    }

    // A checkout that settles an existing balance, or saves a card to charge later, is paid by card alone.
    private static bool AcceptsGiftCards(CheckoutFlow flow)
        => GiftCardCheckoutService.AcceptsGiftCards(flow);
}
