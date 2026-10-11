using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Services;
using Moq;
using OrchardCore.ContentManagement;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Sign-up asks this before starting a checkout, so a plan that can't be sold as a subscription is refused up
/// front instead of being offered for nothing.
/// </summary>
public sealed class SubscriptionPlanPricingTests
{
    [Fact]
    public async Task ResolveAsync_WhenThePlanCannotBePriced_CannotBeBought()
    {
        // Arrange: for example, no currency on the plan and no default currency on its type.
        var resolver = Resolver(result: null);

        // Act
        var pricing = await SubscriptionPlanPricing.ResolveAsync(resolver, Plan(), selection: null);

        // Assert
        Assert.False(pricing.CanBeBought);
        Assert.Equal(SubscriptionPlanPriceProblem.NoPrice, pricing.Problem);
    }

    [Fact]
    public async Task ResolveAsync_ForAOneTimePrice_CannotBeBought()
    {
        // Arrange
        var price = new ProductPrice { PriceId = "once", Currency = "USD", Amount = 10m, Kind = PriceKind.OneTime, IsActive = true };
        var resolver = Resolver(new PriceResult(10m, "USD", 1, price));

        // Act
        var pricing = await SubscriptionPlanPricing.ResolveAsync(resolver, Plan(), new CheckoutPriceSelection { PriceId = "once" });

        // Assert
        Assert.False(pricing.CanBeBought);
        Assert.Equal(SubscriptionPlanPriceProblem.NotRecurring, pricing.Problem);
    }

    [Fact]
    public async Task ResolveAsync_ForAPlanThatRecurs_CanBeBought()
    {
        // Arrange: no named prices, so the plan part decides how it recurs.
        var resolver = Resolver(new PriceResult(9.99m, "USD", 1, null));

        // Act
        var pricing = await SubscriptionPlanPricing.ResolveAsync(resolver, Plan(), selection: null);

        // Assert
        Assert.True(pricing.CanBeBought);
        Assert.Equal(DurationType.Month, pricing.Recurrence.DurationType);
        Assert.Equal(1, pricing.Recurrence.BillingDuration);
    }

    private static IPriceResolver Resolver(PriceResult result)
    {
        var resolver = new Mock<IPriceResolver>();
        resolver
            .Setup(r => r.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        return resolver.Object;
    }

    private static ContentItem Plan()
    {
        var contentItem = new ContentItem { ContentType = "Plan", ContentItemId = "plan-1", DisplayText = "Starter plan" };
        contentItem.Apply(nameof(SubscriptionPart), new SubscriptionPart { BillingDuration = 1, DurationType = DurationType.Month });

        return contentItem;
    }
}
