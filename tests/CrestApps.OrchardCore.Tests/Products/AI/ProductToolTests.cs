using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Products.AI;
using CrestApps.OrchardCore.Products.Core.Feeds;
using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Products.Feeds;
using CrestApps.OrchardCore.Products.Indexes;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Handlers;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Contents;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using OrchardCore.Settings;
using YesSql.Provider.Sqlite;
using YesSql.Sql;
using ISession = YesSql.ISession;
using IStore = YesSql.IStore;
using StoreFactory = YesSql.StoreFactory;

namespace CrestApps.OrchardCore.Tests.Products.AI;

/// <summary>
/// The product and feed tools check the permissions of the pages they stand for, list products as the Products list
/// does, change prices and variants with the product editor's rules and every amount at its currency's scale, and never
/// show the feed's access key. Currencies are a record kind of the generic record tools (see CurrencyRecordKindTests).
/// </summary>
public sealed class ProductToolTests : IAsyncLifetime
{
    private const string FeedKey = "0123456789abcdef0123456789abcdef";

    private static readonly DateTime _now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"crestapps-product-tools-{Guid.NewGuid():N}.db");
    private readonly Dictionary<string, ContentItem> _items = new(StringComparer.Ordinal);
    private readonly List<string> _actions = [];
    private readonly ProductFeedSettings _feedSettings = new() { Key = FeedKey };
    private IStore _store;

    public static TheoryData<string, string> ToolsAndPermissions => new()
    {
        { ListProductsTool.TheName, CommonPermissions.ListContent.Name },
        { UpdateProductPricingTool.TheName, CommonPermissions.EditContent.Name },
        { SaveProductVariantsTool.TheName, CommonPermissions.EditContent.Name },
        { GetProductFeedStatusTool.TheName, ProductFeedPermissions.ManageProductFeeds.Name },
        { RegenerateProductFeedKeyTool.TheName, ProductFeedPermissions.ManageProductFeeds.Name },
    };

    public async ValueTask InitializeAsync()
    {
        _items["mug"] = Product("mug", "Product", "Classic Mug", new ProductPart { Price = 10m, Currency = "USD", Sku = "MUG" });
        _items["shirt"] = Product("shirt", "Shirt", "T-shirt", new ProductPart { Price = 20m, SalePrice = 15m, SaleStartsUtc = _now.AddDays(-1), SaleEndsUtc = _now.AddDays(2) }, new ProductVariantsPart
        {
            Options = [new ProductOption { Name = "Size", Values = ["S", "M"] }],
            Variants =
            [
                new ProductVariant { VariantId = "v-s", Options = new(StringComparer.OrdinalIgnoreCase) { ["Size"] = "S" }, Sku = "SHIRT-S" },
                new ProductVariant { VariantId = "v-m", Options = new(StringComparer.OrdinalIgnoreCase) { ["Size"] = "M" }, Sku = "SHIRT-M" },
            ],
        });
        _items["cap"] = Product("cap", "Product", "Cap", new ProductPart { Price = 8m, Currency = "USD", Sku = "CAP" }, published: false);
        _items["yen"] = Product("yen", "Product", "Tea bowl", new ProductPart { Price = 1200m, Currency = "JPY" });
        _items["dinar"] = Product("dinar", "Product", "Lamp", new ProductPart { Price = 12.5m, Currency = "KWD" });

        _store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={_databasePath};Pooling=False"));
        _store.RegisterIndexes([new ContentItemIndexProvider(), new ProductSkuIndexProvider()]);
        await _store.InitializeAsync(TestContext.Current.CancellationToken);

        await using (var session = _store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(_store.Configuration, transaction);

            await schemaBuilder.CreateMapIndexTableAsync<ContentItemIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("ContentItemVersionId", column => column.WithLength(26))
                .Column<bool>("Published")
                .Column<bool>("Latest")
                .Column<string>("ContentType", column => column.WithLength(255))
                .Column<DateTime>("ModifiedUtc")
                .Column<DateTime>("PublishedUtc")
                .Column<DateTime>("CreatedUtc")
                .Column<string>("Owner", column => column.WithLength(255))
                .Column<string>("Author", column => column.WithLength(255))
                .Column<string>("DisplayText", column => column.WithLength(255)));

            await schemaBuilder.CreateMapIndexTableAsync<ProductSkuIndex>(table => table
                .Column<string>("ContentItemId", column => column.WithLength(26))
                .Column<string>("ContentType", column => column.WithLength(255))
                .Column<string>("Sku", column => column.WithLength(ProductSkuIndex.MaxSkuLength))
                .Column<string>("VariantId", column => column.Nullable().WithLength(26))
                .Column<bool>("Published")
                .Column<bool>("Latest"));

            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        await using (var session = _store.CreateSession())
        {
            foreach (var item in new[] { "mug", "shirt", "cap" })
            {
                await session.SaveAsync(_items[item], cancellationToken: TestContext.Current.CancellationToken);
            }

            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
    }

    public ValueTask DisposeAsync()
    {
        TemporarySqliteDatabase.DisposeAndDelete(_store, _databasePath);

        return ValueTask.CompletedTask;
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName, string permission)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(ProductsAIToolBase.NotAllowedMessage, result);
        Assert.NotNull(permission);
        Assert.Empty(_actions);
        Assert.Equal(FeedKey, _feedSettings.Key);
    }

    [Theory]
    [MemberData(nameof(ToolsAndPermissions))]
    public async Task Tool_RefusesACallerWithoutThePermission(string toolName, string permission)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != permission);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(ProductsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(permission, services.GetRequiredService<FakeAuthorizationService>().Checks.Last().Permission);
        Assert.Empty(_actions);
        Assert.Equal(FeedKey, _feedSettings.Key);
    }

    [Fact]
    public async Task ListProducts_ListsTheLatestVersions_WithPricesSalesAndStock()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(ListProductsTool.TheName, Build(), []));

        // Assert
        var items = json.GetProperty("items").EnumerateArray().ToDictionary(item => item.GetProperty("contentItemId").GetString());

        Assert.Equal(3, json.GetProperty("total").GetInt32());
        Assert.Equal(5, items["mug"].GetProperty("unitsAvailable").GetInt32());
        Assert.Equal("USD", items["mug"].GetProperty("currency").GetString());
        Assert.Equal("EUR", items["shirt"].GetProperty("currency").GetString());
        Assert.True(items["shirt"].GetProperty("onSaleNow").GetBoolean());
        Assert.Equal(2, items["shirt"].GetProperty("variants").GetInt32());
        Assert.Equal("draft", items["cap"].GetProperty("status").GetString());
    }

    [Fact]
    public async Task ListProducts_FiltersBySkuOfAVariant_ByOnSale_ByStatus_AndByTheTypesTheCallerMayView()
    {
        // Act
        var bySku = AgentToolTestContext.ReadJson(await InvokeAsync(ListProductsTool.TheName, Build(), new() { ["sku"] = "SHIRT-M" }));
        var onSale = AgentToolTestContext.ReadJson(await InvokeAsync(ListProductsTool.TheName, Build(), new() { ["onSale"] = true }));
        var drafts = AgentToolTestContext.ReadJson(await InvokeAsync(ListProductsTool.TheName, Build(), new() { ["status"] = "draft" }));
        var viewable = AgentToolTestContext.ReadJson(await InvokeAsync(ListProductsTool.TheName, Build(authorize: (permission, resource) =>
            permission.Name != CommonPermissions.ViewContent.Name || (resource as ContentItem)?.ContentType != "Shirt"), []));
        var unknownType = await InvokeAsync(ListProductsTool.TheName, Build(), new() { ["contentType"] = "Article" });

        // Assert
        var shirt = Assert.Single(bySku.GetProperty("items").EnumerateArray());

        Assert.Equal("v-m", shirt.GetProperty("matchedVariantId").GetString());
        Assert.Equal("shirt", Assert.Single(onSale.GetProperty("items").EnumerateArray()).GetProperty("contentItemId").GetString());
        Assert.Equal("cap", Assert.Single(drafts.GetProperty("items").EnumerateArray()).GetProperty("contentItemId").GetString());
        Assert.Equal(2, viewable.GetProperty("total").GetInt32());
        Assert.StartsWith("Nothing was listed: 'Article' is not a product type", unknownType, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UpdateProductPricing_PutsAProductOnSale_InADraft_WithItsDatesInUtc()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(UpdateProductPricingTool.TheName, Build(), new()
        {
            ["contentItemId"] = "mug",
            ["salePrice"] = 7.99m,
            ["saleStarts"] = "2026-11-20T09:00",
            ["saleEnds"] = "2026-11-22T23:00",
        }));

        // Assert
        var part = Part<ProductPart>(_items["mug"]);

        Assert.False(json.GetProperty("published").GetBoolean());
        Assert.Equal(["validate", "saveDraft"], _actions);
        Assert.Equal(7.99m, part.SalePrice);
        Assert.Equal(new DateTime(2026, 11, 20, 9, 0, 0, DateTimeKind.Utc), part.SaleStartsUtc);
        Assert.Equal(10m, part.Price);
    }

    [Theory]
    [InlineData("mug", "salePrice", 12, "The sale price has to be lower than the price (10).")]
    [InlineData("mug", "price", -1, "price cannot be below zero")]
    [InlineData("yen", "price", 1200.5, "JPY has no smaller unit")]
    [InlineData("mug", "price", 9.999, "USD has 2 decimal places")]
    public async Task UpdateProductPricing_RefusesWhatTheEditorOrTheCurrencyRefuses(string product, string field, double amount, string expected)
    {
        // Act
        var result = await InvokeAsync(UpdateProductPricingTool.TheName, Build(), new() { ["contentItemId"] = product, [field] = (decimal)amount });

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task UpdateProductPricing_KeepsThreeDecimalsForADinarPrice_AndPublishesOnlyWithThePermission()
    {
        // Act
        var refused = await InvokeAsync(UpdateProductPricingTool.TheName, Build(authorize: (permission, _) => permission.Name != CommonPermissions.PublishContent.Name), new() { ["contentItemId"] = "dinar", ["price"] = 12.345m, ["publish"] = true });
        var published = AgentToolTestContext.ReadJson(await InvokeAsync(UpdateProductPricingTool.TheName, Build(), new() { ["contentItemId"] = "dinar", ["price"] = 12.345m, ["publish"] = true }));

        // Assert
        Assert.StartsWith("Nothing was changed: publishing the change needs the permission", refused, StringComparison.Ordinal);
        Assert.True(published.GetProperty("published").GetBoolean());
        Assert.Equal(12.345m, Part<ProductPart>(_items["dinar"]).Price);
        Assert.Equal(["validate", "publish"], _actions);
    }

    [Fact]
    public async Task UpdateProductPricing_ChangesOneVariant_AndRefusesSaleDatesOnAVariant()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(UpdateProductPricingTool.TheName, Build(), new() { ["contentItemId"] = "shirt", ["variantId"] = "v-m", ["price"] = 22m }));
        var dates = await InvokeAsync(UpdateProductPricingTool.TheName, Build(), new() { ["contentItemId"] = "shirt", ["variantId"] = "v-m", ["saleEnds"] = "2026-12-01T00:00" });

        // Assert
        Assert.Equal("SHIRT-M", json.GetProperty("sku").GetString());
        Assert.Equal(22m, Part<ProductVariantsPart>(_items["shirt"]).Variants.Single(variant => variant.VariantId == "v-m").Price);
        Assert.StartsWith("Nothing was changed: a variant has no sale dates of its own", dates, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveProductVariants_GeneratesTheVariants_KeepsTheirIds_AndSetsAVariantFoundByItsOptions()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveProductVariantsTool.TheName, Build(), new()
        {
            ["contentItemId"] = "shirt",
            ["options"] = JsonElement("""{ "Size": ["S", "M", "L"], "Color": ["Blue"] }"""),
            ["variants"] = JsonElement("""[{ "options": { "Size": "L", "Color": "Blue" }, "sku": "SHIRT-L", "price": 24 }]"""),
        }));

        // Assert
        var variants = Part<ProductVariantsPart>(_items["shirt"]).Variants;

        Assert.Equal(3, variants.Count);
        Assert.Contains(variants, variant => variant.VariantId == "v-s" && variant.Sku == "SHIRT-S");
        Assert.Contains(variants, variant => variant.Sku == "SHIRT-L" && variant.Price == 24m && variant.VariantId is not "v-s" and not "v-m");
        Assert.Empty(json.GetProperty("removed").EnumerateArray());
        Assert.Equal(["validate", "saveDraft"], _actions);
    }

    [Fact]
    public async Task SaveProductVariants_ReportsTheVariantsRemoved()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveProductVariantsTool.TheName, Build(), new()
        {
            ["contentItemId"] = "shirt",
            ["options"] = JsonElement("""{ "Size": ["S"] }"""),
        }));

        // Assert
        var removed = Assert.Single(json.GetProperty("removed").EnumerateArray());

        Assert.Equal("SHIRT-M", removed.GetProperty("sku").GetString());
        Assert.Single(Part<ProductVariantsPart>(_items["shirt"]).Variants);
    }

    [Theory]
    [InlineData("""{ "Material": ["Cotton"] }""", null, "has no option 'Material'")]
    [InlineData(null, """[{ "variantId": "v-m", "sku": "SHIRT-S" }]""", "the SKU 'SHIRT-S' is used by more than one variant")]
    [InlineData(null, """[{ "variantId": "v-m", "price": -2 }]""", "enter a price of zero or more")]
    [InlineData(null, """[{ "variantId": "v-m", "imagePath": "elsewhere/photo.jpg" }]""", "has no photos")]
    [InlineData(null, """[{ "variantId": "v-x", "sku": "X" }]""", "names no variant of the product")]
    public async Task SaveProductVariants_RefusesWhatTheEditorRefuses_AndSavesNothing(string options, string variants, string expected)
    {
        // Arrange
        var arguments = new Dictionary<string, object> { ["contentItemId"] = "shirt" };

        if (options is not null)
        {
            arguments["options"] = JsonElement(options);
        }

        if (variants is not null)
        {
            arguments["variants"] = JsonElement(variants);
        }

        // Act
        var result = await InvokeAsync(SaveProductVariantsTool.TheName, Build(), arguments);

        // Assert
        Assert.Contains(expected, result, StringComparison.Ordinal);
        Assert.Empty(_actions);
        Assert.Equal(["SHIRT-S", "SHIRT-M"], Part<ProductVariantsPart>(_items["shirt"]).Variants.Select(variant => variant.Sku));
    }

    [Fact]
    public async Task GetProductFeedStatus_ReportsTheFeed_WithoutItsKey()
    {
        // Act
        var result = await InvokeAsync(GetProductFeedStatusTool.TheName, Build(), []);

        // Assert
        var json = AgentToolTestContext.ReadJson(result);

        Assert.DoesNotContain(FeedKey, result, StringComparison.Ordinal);
        Assert.True(json.GetProperty("hasKey").GetBoolean());
        Assert.Equal("https://shop.example/feeds/products.xml", json.GetProperty("xmlAddress").GetString());
        Assert.Equal(2, json.GetProperty("items").GetInt32());
        Assert.Equal(1, json.GetProperty("leftOut").GetInt32());
        Assert.Equal("Missing a GTIN.", json.GetProperty("problems")[0].GetProperty("problem").GetString());
    }

    [Fact]
    public async Task RegenerateProductFeedKey_MakesANewKeyItNeverShows_AndRemovesIt()
    {
        // Arrange
        var services = Build();

        // Act
        var renewed = await InvokeAsync(RegenerateProductFeedKeyTool.TheName, services, new() { ["action"] = "new" });
        var newKey = _feedSettings.Key;
        var removed = AgentToolTestContext.ReadJson(await InvokeAsync(RegenerateProductFeedKeyTool.TheName, services, new() { ["action"] = "remove" }));
        var status = AgentToolTestContext.ReadJson(await InvokeAsync(GetProductFeedStatusTool.TheName, services, []));
        var wrong = await InvokeAsync(RegenerateProductFeedKeyTool.TheName, Build(), new() { ["action"] = "rotate" });

        // Assert
        Assert.NotEqual(FeedKey, newKey);
        Assert.Equal(32, newKey.Length);
        Assert.DoesNotContain(newKey, renewed, StringComparison.Ordinal);
        Assert.False(removed.GetProperty("hasKey").GetBoolean());
        Assert.Null(_feedSettings.Key);
        Assert.False(status.GetProperty("hasKey").GetBoolean());
        Assert.StartsWith("Nothing was changed: give action new", wrong, StringComparison.Ordinal);
    }

    private static TPart Part<TPart>(ContentItem item)
        where TPart : ContentPart
        => item.TryGet<TPart>(out var part) ? part : null;

    private static JsonElement JsonElement(string json)
        => JsonSerializer.Deserialize<JsonElement>(json);

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            UpdateProductPricingTool.TheName => new() { ["contentItemId"] = "mug", ["price"] = 11m },
            SaveProductVariantsTool.TheName => new() { ["contentItemId"] = "shirt", ["options"] = JsonElement("""{ "Size": ["S"] }""") },
            RegenerateProductFeedKeyTool.TheName => new() { ["action"] = "new" },
            _ => [],
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var types = new[] { TypeDefinition("Product", "USD", variants: false), TypeDefinition("Shirt", "EUR", variants: true) };

        var definitions = new Mock<IContentDefinitionManager>();
        definitions.Setup(manager => manager.ListTypeDefinitionsAsync()).ReturnsAsync(types);
        definitions.Setup(manager => manager.GetTypeDefinitionAsync(It.IsAny<string>())).ReturnsAsync((string name) => types.FirstOrDefault(type => type.Name == name));
        services.AddSingleton(definitions.Object);
        services.AddSingleton<ProductContentTypes>();

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync(It.IsAny<string>(), It.IsAny<VersionOptions>()))
            .ReturnsAsync((string id, VersionOptions options) => _items.TryGetValue(id, out var item) && (item.Published || !options.IsPublished) ? item : null);
        contentManager.Setup(manager => manager.UpdateAsync(It.IsAny<ContentItem>())).Returns(Task.CompletedTask);
        contentManager.Setup(manager => manager.ValidateAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("validate")).ReturnsAsync(new ContentValidateResult());
        contentManager.Setup(manager => manager.SaveDraftAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("saveDraft")).Returns(Task.CompletedTask);
        contentManager.Setup(manager => manager.PublishAsync(It.IsAny<ContentItem>())).Callback(() => _actions.Add("publish")).ReturnsAsync(true);
        services.AddSingleton(contentManager.Object);

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(_now);
        services.AddSingleton(clock.Object);
        services.AddSingleton<IProductAvailabilityProvider>(new MugStock());
        services.AddSingleton<ISession>(_ => _store.CreateSession());

        var site = new Mock<ISite>();
        site.Setup(value => value.BaseUrl).Returns("https://shop.example");
        site.Setup(value => value.Properties).Returns(new JsonObject());
        site.Setup(value => value.GetOrCreate<ProductFeedSettings>()).Returns(_feedSettings);
        var siteService = new Mock<ISiteService>();
        siteService.Setup(value => value.GetSiteSettingsAsync()).ReturnsAsync(site.Object);
        siteService.Setup(value => value.LoadSiteSettingsAsync()).ReturnsAsync(site.Object);
        services.AddSingleton(siteService.Object);

        var feed = new Mock<IProductFeedService>();
        feed.Setup(value => value.GetAsync(null, true, It.IsAny<CancellationToken>())).ReturnsAsync(new ProductFeed(
            [new ProductFeedItem { Id = "mug", Title = "Classic Mug" }, new ProductFeedItem { Id = "shirt-s", Title = "T-shirt S" }],
            [new ProductFeedProblem("cap", "Cap", "Missing a GTIN.", LeftOut: true)],
            _now));
        services.AddSingleton(feed.Object);
        services.AddScoped<ProductFeedToolScope>();

        return services.BuildServiceProvider();
    }

    private static ContentTypeDefinition TypeDefinition(string name, string currency, bool variants)
    {
        var parts = new List<ContentTypePartDefinition>
        {
            new(nameof(ProductPart), new ContentPartDefinition(nameof(ProductPart), [], []), new JsonObject
            {
                [nameof(ProductPartSettings)] = JsonSerializer.SerializeToNode(new ProductPartSettings { Type = ProductType.Good, DefaultCurrency = currency }),
            }),
        };

        if (variants)
        {
            parts.Add(new ContentTypePartDefinition(nameof(ProductVariantsPart), new ContentPartDefinition(nameof(ProductVariantsPart), [], []), new JsonObject
            {
                [nameof(ProductVariantsPartSettings)] = JsonSerializer.SerializeToNode(new ProductVariantsPartSettings
                {
                    Options = [new ProductVariantOptionSettings { Name = "Size" }, new ProductVariantOptionSettings { Name = "Color" }],
                }),
            }));
        }

        var type = new ContentTypeDefinition(name, name, parts, []);

        foreach (var part in type.Parts)
        {
            part.ContentTypeDefinition = type;
        }

        return type;
    }

    private static ContentItem Product(string id, string type, string title, ProductPart product, ProductVariantsPart variants = null, bool published = true)
    {
        var item = new ContentItem
        {
            ContentItemId = id,
            ContentItemVersionId = id + "-v1",
            ContentType = type,
            DisplayText = title,
            Latest = true,
            Published = published,
            ModifiedUtc = _now,
        };

        item.Apply(nameof(ProductPart), product);

        if (variants is not null)
        {
            item.Apply(nameof(ProductVariantsPart), variants);
        }

        return item;
    }

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListProductsTool.TheName => new ListProductsTool(),
            UpdateProductPricingTool.TheName => new UpdateProductPricingTool(),
            SaveProductVariantsTool.TheName => new SaveProductVariantsTool(),
            GetProductFeedStatusTool.TheName => new GetProductFeedStatusTool(),
            _ => new RegenerateProductFeedKeyTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }

    private sealed class MugStock : IProductAvailabilityProvider
    {
        public Task<IReadOnlyDictionary<ProductStockKey, ProductAvailability>> GetAvailabilityAsync(ProductAvailabilityRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<ProductStockKey, ProductAvailability>>(request.Products
                .Where(key => key.ContentItemId == "mug")
                .ToDictionary(key => key, _ => new ProductAvailability(5, false, 2)));
    }
}
