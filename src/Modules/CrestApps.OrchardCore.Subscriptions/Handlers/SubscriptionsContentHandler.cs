using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class SubscriptionsContentHandler : ContentHandlerBase
{
    private readonly IStripeProductService _stripeProductService;
    private readonly IStripePlanService _stripePlanService;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public SubscriptionsContentHandler(
        IStripeProductService stripeProductService,
        IStripePlanService stripePlanService,
        IContentDefinitionManager contentDefinitionManager)
    {
        _stripeProductService = stripeProductService;
        _stripePlanService = stripePlanService;
        _contentDefinitionManager = contentDefinitionManager;
    }

    public override Task PublishedAsync(PublishContentContext context)
        => UpdateStripeAsync(context);

    public override async Task UnpublishedAsync(PublishContentContext context)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

        if (definition == null ||
            !definition.StereotypeEquals(SubscriptionsConstants.Stereotype) ||
            !context.ContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
        {
            return;
        }

        var plan = await _stripePlanService.GetAsync(context.ContentItem.ContentItemVersionId);

        if (plan == null)
        {
            return;
        }

        var planRequest = new UpdatePlanRequest()
        {
            Title = context.ContentItem.DisplayText,
            IsActive = false,
        };

        await _stripePlanService.UpdateAsync(context.ContentItem.ContentItemVersionId, planRequest);
    }

    private async Task UpdateStripeAsync(PublishContentContext context)
    {
        var definition = await _contentDefinitionManager.GetTypeDefinitionAsync(context.ContentItem.ContentType);

        if (definition == null ||
            !definition.StereotypeEquals(SubscriptionsConstants.Stereotype) ||
            !context.ContentItem.TryGet<SubscriptionPart>(out var subscriptionPart))
        {
            return;
        }

        var plan = await _stripePlanService.GetAsync(context.ContentItem.ContentItemVersionId);

        if (plan != null)
        {
            await _stripePlanService.UpdateAsync(context.ContentItem.ContentItemVersionId, new UpdatePlanRequest()
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

        var planRequest = new CreatePlanRequest()
        {
            Id = context.ContentItem.ContentItemVersionId,
            ProductId = product.Id,
            Title = context.ContentItem.DisplayText,
            Amount = subscriptionPart.BillingAmount,
            Currency = "USD",
            IntervalCount = subscriptionPart.BillingDuration,
            Interval = subscriptionPart.DurationType.ToString(),
            // TODO, configure Currency, BillingCycleLimit and SubscriptionDayDelay
        };

        await _stripePlanService.CreateAsync(planRequest);
    }
}
