using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Records;
using OrchardCore.ContentTypes.Events;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// A subscription plan is a product, so the product listing lists it like any other.
/// </summary>
public sealed class SubscriptionPlansAreProductsTests
{
    [Fact]
    public void SubscriptionStereotype_AddsTheProductPart_AndTheSubscriptionPart()
    {
        var record = CreateTypeRecord("Subscription");

        new SubscriptionPartContentTypeDefinitionHandler().ContentTypeBuilding(new ContentTypeBuildingContext("Plan", record));

        Assert.Contains(record.ContentTypePartDefinitionRecords, part => part.PartName == nameof(ProductPart));
        Assert.Contains(record.ContentTypePartDefinitionRecords, part => part.PartName == nameof(SubscriptionPart));
    }

    [Fact]
    public void SubscriptionStereotype_KeepsAProductPartTheTypeAlreadyHas()
    {
        var record = CreateTypeRecord("Subscription");
        record.ContentTypePartDefinitionRecords.Add(new ContentTypePartDefinitionRecord { Name = nameof(ProductPart), PartName = nameof(ProductPart), Settings = [] });

        new SubscriptionPartContentTypeDefinitionHandler().ContentTypeBuilding(new ContentTypeBuildingContext("Plan", record));

        Assert.Single(record.ContentTypePartDefinitionRecords, part => part.PartName == nameof(ProductPart));
    }

    [Fact]
    public void OtherStereotypes_AreLeftAlone()
    {
        var record = CreateTypeRecord("Widget");

        new SubscriptionPartContentTypeDefinitionHandler().ContentTypeBuilding(new ContentTypeBuildingContext("Banner", record));

        Assert.Empty(record.ContentTypePartDefinitionRecords);
    }

    [Theory]
    [InlineData(DurationType.Month, 1, BillingInterval.Month, 1)]
    [InlineData(DurationType.Year, 1, BillingInterval.Year, 1)]
    [InlineData(DurationType.Week, 2, BillingInterval.Week, 2)]
    [InlineData(DurationType.Day, 0, BillingInterval.Day, 1)]
    public async Task Contributor_GivesAPlanItsBillingPeriod(DurationType durationType, int billingDuration, BillingInterval interval, int duration)
    {
        var plan = CreatePlan(durationType, billingDuration);
        var document = new ProductSearchDocument { Price = 29m };

        await new SubscriptionProductSearchDocumentContributor().ContributeAsync(plan, document);

        Assert.Equal(interval, document.BillingInterval);
        Assert.Equal(duration, document.BillingDuration);
    }

    [Fact]
    public async Task Contributor_KeepsTheIntervalOfARecurringOfferedPrice()
    {
        // A plan sold through the product prices part already carries its default price's interval.
        var plan = CreatePlan(DurationType.Month, 1);
        var document = new ProductSearchDocument { Price = 290m, BillingInterval = BillingInterval.Year, BillingDuration = 1 };

        await new SubscriptionProductSearchDocumentContributor().ContributeAsync(plan, document);

        Assert.Equal(BillingInterval.Year, document.BillingInterval);
    }

    [Fact]
    public async Task Contributor_AddsNoPeriod_WhenThePriceIsNotShown()
    {
        var plan = CreatePlan(DurationType.Month, 1);
        var document = new ProductSearchDocument { Price = null };

        await new SubscriptionProductSearchDocumentContributor().ContributeAsync(plan, document);

        Assert.Null(document.BillingInterval);
    }

    [Fact]
    public async Task Contributor_GivesAPlanItsPosition_ForTheRecommendedSort()
    {
        var plan = new ContentItem { ContentType = "Plan", ContentItemId = "plan" };
        plan.Apply(nameof(SubscriptionPart), new SubscriptionPart { DurationType = DurationType.Month, BillingDuration = 1, Sort = 3 });
        var document = new ProductSearchDocument { Price = 29m };

        await new SubscriptionProductSearchDocumentContributor().ContributeAsync(plan, document);

        Assert.Contains(document.Attributes, value => value.Key == ProductAttributeKeys.Position && value.Number == 3);
    }

    [Fact]
    public async Task Contributor_AddsNoPosition_WhenThePlanHasNoSortValue()
    {
        var plan = CreatePlan(DurationType.Month, 1);
        var document = new ProductSearchDocument { Price = 29m };

        await new SubscriptionProductSearchDocumentContributor().ContributeAsync(plan, document);

        Assert.DoesNotContain(document.Attributes, value => value.Key == ProductAttributeKeys.Position);
    }

    private static ContentTypeDefinitionRecord CreateTypeRecord(string stereotype)
        => new()
        {
            Name = "Plan",
            Settings = new JsonObject
            {
                ["ContentTypeSettings"] = new JsonObject { ["Stereotype"] = stereotype },
            },
            ContentTypePartDefinitionRecords = [],
        };

    private static ContentItem CreatePlan(DurationType durationType, int billingDuration)
    {
        var plan = new ContentItem { ContentType = "Plan", ContentItemId = "plan" };
        plan.Apply(nameof(SubscriptionPart), new SubscriptionPart { DurationType = durationType, BillingDuration = billingDuration });

        return plan;
    }
}
