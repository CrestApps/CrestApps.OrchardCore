using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Taxation;
using CrestApps.OrchardCore.Taxation.Core;
using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Taxation;

public sealed class ContentItemTaxableItemProviderTests
{
    private static TaxTestHarness CreateHarness()
        => new(new TestClock(TaxTestData.TransactionDate), services => services.AddTaxableItemProvider<ContentItemTaxableItemProvider>());

    private static ContentItem CreateTelevision(decimal price)
    {
        var contentItem = new ContentItem
        {
            ContentType = "Television",
            ContentItemId = "tv-1",
        };

        contentItem.Weld(nameof(TaxationPart), new TaxationPart
        {
            Taxable = true,
            TaxCategoryCode = "Electronics",
            TaxClassificationCode = "Television",
        });

        contentItem.Content["ProductPart"] = new JsonObject
        {
            ["Price"] = JsonValue.Create(price),
        };

        return contentItem;
    }

    [Fact]
    public async Task Resolver_ConvertsTaxableContentItemIntoTaxableItem()
    {
        var harness = CreateHarness();
        var resolver = harness.GetService<ITaxableItemResolver>();

        var item = await resolver.ResolveAsync(CreateTelevision(500m), TestContext.Current.CancellationToken);

        Assert.NotNull(item);
        Assert.Equal("tv-1", item.Id);
        Assert.Equal(500m, item.UnitPrice);
        Assert.Equal("Electronics", item.TaxCategoryCode);
        Assert.Equal("Television", item.TaxClassificationCode);
    }

    [Fact]
    public async Task CustomContentType_ParticipatesInTaxationWithoutCustomLogic()
    {
        var harness = CreateHarness();
        var jurisdictionId = await TaxTestData.AddJurisdictionAsync(harness, "California", "US", "CA");

        await TaxTestData.AddRuleAsync(harness, new TaxRule
        {
            Name = "Electronics tax",
            JurisdictionId = jurisdictionId,
            CategoryCode = "Electronics",
            CalculationMethod = TaxCalculationMethodNames.Percentage,
            Rate = 0.075m,
        });

        var resolver = harness.GetService<ITaxableItemResolver>();
        var item = await resolver.ResolveAsync(CreateTelevision(500m), TestContext.Current.CancellationToken);

        var context = new TaxCalculationContext
        {
            Currency = "USD",
            TransactionDateUtc = TaxTestData.TransactionDate,
            Destination = TaxTestData.California(),
            Items = [item],
        };

        var result = await harness.TaxService.CalculateAsync(context, TestContext.Current.CancellationToken);

        var line = Assert.Single(result.Lines);
        Assert.Equal(37.5m, line.TaxAmount);
        Assert.Equal(537.5m, result.TotalAmount);
    }

    [Fact]
    public async Task NonTaxableContentItem_IsNotResolved()
    {
        var harness = CreateHarness();
        var resolver = harness.GetService<ITaxableItemResolver>();

        var contentItem = new ContentItem
        {
            ContentType = "Article",
            ContentItemId = "article-1",
        };

        var item = await resolver.ResolveAsync(contentItem, TestContext.Current.CancellationToken);

        Assert.Null(item);
    }
}
