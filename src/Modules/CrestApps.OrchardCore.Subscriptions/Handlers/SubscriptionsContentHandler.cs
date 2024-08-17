using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class SubscriptionsContentHandler : ContentHandlerBase
{
    private readonly ISiteService _siteService;
    private readonly IStripeProductService _stripeProductService;
    private readonly IStripePriceService _stripePriceService;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionsContentHandler(
        ISiteService siteService,
        IStripeProductService stripeProductService,
        IStripePriceService stripePriceService,
        IContentDefinitionManager contentDefinitionManager)
    {
        _siteService = siteService;
        _stripeProductService = stripeProductService;
        _stripePriceService = stripePriceService;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override Task PublishedAsync(PublishContentContext context)
        => UpdateStripeAsync(context);

    public override async Task UnpublishedAsync(PublishContentContext context)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

        if (definition == null ||
            !definition.StereotypeEquals(SubscriptionConstants.Stereotype) ||
            !context.ContentItem.TryGet<SubscriptionPart>(out _))
        {
            return;
        }

        var price = await _stripePriceService.GetAsync(context.ContentItem.ContentItemVersionId);

        if (price == null)
        {
            return;
        }

        var priceRequest = new UpdatePriceRequest()
        {
            Title = context.ContentItem.DisplayText,
            IsActive = false,
        };

        await _stripePriceService.UpdateAsync(context.ContentItem.ContentItemVersionId, priceRequest);
    }

    private async Task UpdateStripeAsync(PublishContentContext context)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

        if (definition == null ||
            !definition.StereotypeEquals(SubscriptionConstants.Stereotype) ||
            !context.ContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
        {
            return;
        }

        var plan = await _stripePriceService.GetAsync(context.ContentItem.ContentItemVersionId);

        if (plan != null)
        {
            await _stripePriceService.UpdateAsync(context.ContentItem.ContentItemVersionId, new UpdatePriceRequest()
            {
                Title = context.ContentItem.DisplayText,
                IsActive = true,
            });

            return;
        }

        var product = await _stripeProductService.GetAsync(definition.Name);

        if (product == null)
        {
            var productRequest = new CreateProductRequest()
            {
                Id = definition.Name,
                Title = definition.DisplayName,
                Description = definition.GetDescription(),
                Type = "service",
            };

            product = await _stripeProductService.CreateAsync(productRequest);
        }

        var settings = await _siteService.GetSettingsAsync<SubscriptionSettings>();
        var priceRequest = new CreatePriceRequest()
        {
            LookupKey = context.ContentItem.ContentItemVersionId,
            ProductId = product.Id,
            Title = context.ContentItem.DisplayText,
            Amount = subscriptionPart.BillingAmount,
            Currency = settings.Currency,
            IntervalCount = subscriptionPart.BillingDuration,
            Interval = subscriptionPart.DurationType.ToString().ToLowerInvariant(),
        };

        await _stripePriceService.CreateAsync(priceRequest);
    }
}
