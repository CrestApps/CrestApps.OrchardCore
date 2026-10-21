using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Handlers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using OrchardCore.ContentManagement;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// The order bump on the payment step: the product the store offers, at its offer price, which one tick adds to the
/// order before paying. It's offered on orders that don't have it yet, in the order's currency, while it's in stock.
/// </summary>
public sealed class OrderBumpCheckoutFlowDisplayDriver : CheckoutFlowDisplayDriver
{
    private readonly ISiteService _siteService;
    private readonly IContentManager _contentManager;
    private readonly IProductSnapshotResolver _snapshotResolver;
    private readonly IEnumerable<IProductAvailabilityProvider> _availability;
    private readonly CheckoutInvoiceBuilder _invoiceBuilder;

    public OrderBumpCheckoutFlowDisplayDriver(
        ISiteService siteService,
        IContentManager contentManager,
        IProductSnapshotResolver snapshotResolver,
        IEnumerable<IProductAvailabilityProvider> availability,
        CheckoutInvoiceBuilder invoiceBuilder)
    {
        _siteService = siteService;
        _contentManager = contentManager;
        _snapshotResolver = snapshotResolver;
        _availability = availability;
        _invoiceBuilder = invoiceBuilder;
    }

    protected override string StepKey
        => CheckoutConstants.PaymentStepKey;

    protected override async Task<IDisplayResult> EditStepAsync(CheckoutFlow flow, BuildEditorContext context)
    {
        var settings = await _siteService.GetSettingsAsync<OrderSettings>();
        var added = flow.Session.TryGet<CheckoutOrderBump>(out var bump) ? bump : null;
        var offer = added is null ? (await GetOfferAsync(flow)).Offer : new Offered(added.Title, added.UnitPrice, added.ListPrice, added);

        if (offer is null)
        {
            return null;
        }

        return Initialize<OrderBumpViewModel>("CheckoutOrderBump", model =>
        {
            model.Headline = settings.OrderBumpHeadline;
            model.Title = offer.Title;
            model.Price = ProductPriceFormatter.Format(offer.UnitPrice, flow.Session.Currency);
            model.ListPrice = offer.ListPrice > offer.UnitPrice ? ProductPriceFormatter.Format(offer.ListPrice, flow.Session.Currency) : null;
            model.Accept = added is not null;
        }).Location("Aside:4");
    }

    protected override async Task<IDisplayResult> UpdateStepAsync(CheckoutFlow flow, UpdateEditorContext context)
    {
        var model = new OrderBumpViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var orderStep = flow.Session.Steps.FirstOrDefault(step => step.Key == OrderConstants.CheckoutStepKey);
        var added = flow.Session.TryGet<CheckoutOrderBump>(out var existing) ? existing : null;

        if (orderStep is null)
        {
            return await EditStepAsync(flow, context);
        }

        if (model.Accept && added is null && (await GetOfferAsync(flow)).Offer is { } offer)
        {
            flow.Session.Put(offer.Bump);
            orderStep.BillingItems = [.. (orderStep.BillingItems ?? []).Where(item => item.ItemId != CheckoutOrderBump.BillingItemId), OrderCheckoutHandler.ToBillingItem(offer.Bump)];

            // The total changes before the buyer pays, so they see what the offer adds.
            await _invoiceBuilder.BuildAsync(flow);
        }
        else if (!model.Accept && added is not null)
        {
            flow.Session.Properties?.Remove(nameof(CheckoutOrderBump));
            orderStep.BillingItems = [.. (orderStep.BillingItems ?? []).Where(item => item.ItemId != CheckoutOrderBump.BillingItemId)];

            await _invoiceBuilder.BuildAsync(flow);
        }

        return await EditStepAsync(flow, context);
    }

    // The offer, when the store has one this order can take.
    private async Task<(OrderSettings Settings, Offered Offer)> GetOfferAsync(CheckoutFlow flow)
    {
        var settings = await _siteService.GetSettingsAsync<OrderSettings>();

        if (string.IsNullOrEmpty(settings.OrderBumpProductId) ||
            flow.Session is not CheckoutSession { ReferenceType: CheckoutReferenceTypes.Order } session ||
            session.Steps.FirstOrDefault(step => step.Key == OrderConstants.CheckoutStepKey) is not { } orderStep ||
            (orderStep.BillingItems ?? []).Any(item => item.ProductId == settings.OrderBumpProductId))
        {
            return (settings, null);
        }

        var item = await _contentManager.GetAsync(settings.OrderBumpProductId, VersionOptions.Published);
        var product = item is null ? null : await _snapshotResolver.ResolveAsync(new ProductSnapshotContext(item));

        if (product is null || !string.Equals(product.Currency, session.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return (settings, null);
        }

        // A product sold in variants or with a required add-on has a choice to make, which the payment step can't.
        if ((item.TryGet<ProductVariantsPart>(out var variants) && variants.HasVariants) ||
            (item.TryGet<ProductPart>(out var productPart) && productPart.AddOns.Any(addOn => addOn.Required)))
        {
            return (settings, null);
        }

        var stock = (await _availability.GetAvailabilityAsync([new ProductStockKey(item.ContentItemId, null)])).GetValueOrDefault(new ProductStockKey(item.ContentItemId, null));

        if (stock is not null && !stock.CanBuy(1))
        {
            return (settings, null);
        }

        var price = settings.OrderBumpPercentOff is > 0 and < 100 and var percent
            ? Math.Round(product.UnitPrice * (100m - percent) / 100m, 2, MidpointRounding.AwayFromZero)
            : product.UnitPrice;

        return (settings, new Offered(product.Title, price, product.UnitPrice, new CheckoutOrderBump
        {
            ContentItemId = item.ContentItemId,
            ContentItemVersionId = product.ContentItemVersionId,
            ContentType = product.ContentType,
            Title = product.Title,
            Sku = product.Sku,
            UnitPrice = price,
            ListPrice = product.UnitPrice,
            ProductType = product.ProductType,
            TaxCategoryCode = product.TaxCategoryCode,
            TaxClassificationCode = product.TaxClassificationCode,
            CategoryIds = ProductCategories.GetIds(item),
        }));
    }

    private sealed record Offered(string Title, decimal UnitPrice, decimal ListPrice, CheckoutOrderBump Bump);
}
