using System.Net.Http;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce.Feeds;

/// <summary>
/// The products pushed to an ACP feed API have ACP's shapes: a product per store product with its items as variants,
/// prices in minor units and uppercase currencies, availability, barcodes, options and the seller's links; and every
/// call carries the bearer key, the API version and an idempotency key.
/// </summary>
public sealed class AcpFeedPayloadTests
{
    private static readonly DateTime _now = new(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void AProductWithVariants_IsOneProduct_WithEachItemAsAVariant()
    {
        // Arrange
        var black = OpenAIProductFeedWriterTests.Variant();
        var white = OpenAIProductFeedWriterTests.Variant();

        white.Id = "SHOE-WHT-10";
        white.VariantId = "V2";
        white.Availability = "out_of_stock";
        white.SalePrice = null;
        white.VariantOptions = new Dictionary<string, string> { ["Color"] = "White", ["Size"] = "10" };

        // Act
        var products = AcpFeedProductMapper.ToProducts([black, white, OpenAIProductFeedWriterTests.Simple()], _now);

        // Assert
        Assert.Equal(2, products.Count);

        var product = products[0];
        var variants = product["variants"].AsArray();
        var first = variants[0].AsObject();

        Assert.Equal("PRODUCT1", product["id"].GetValue<string>());
        Assert.Equal("Runner", product["title"].GetValue<string>());
        Assert.Equal("A light running shoe.", product["description"]["plain"].GetValue<string>());
        Assert.Equal(3, product["media"].AsArray().Count);
        Assert.Equal(2, variants.Count);

        Assert.Equal("SHOE-BLK-10", first["id"].GetValue<string>());
        Assert.Equal(5950, first["price"]["amount"].GetValue<long>());
        Assert.Equal("USD", first["price"]["currency"].GetValue<string>());
        Assert.Equal(7999, first["list_price"]["amount"].GetValue<long>());
        Assert.True(first["availability"]["available"].GetValue<bool>());
        Assert.Equal("preorder", first["availability"]["status"].GetValue<string>());
        Assert.Equal("gtin", first["barcodes"][0]["type"].GetValue<string>());
        Assert.Equal("4006381333931", first["barcodes"][0]["value"].GetValue<string>());
        Assert.Equal("new", first["condition"][0].GetValue<string>());
        Assert.Equal("Apparel > Shoes", first["categories"][0]["value"].GetValue<string>());
        Assert.Contains(first["variant_options"].AsArray(), option => option["name"].GetValue<string>() == "Color" && option["value"].GetValue<string>() == "Black");
        Assert.Equal("Contoso", first["seller"]["name"].GetValue<string>());
        Assert.Contains(first["seller"]["links"].AsArray(), link => link["type"].GetValue<string>() == "terms_of_use" && link["url"].GetValue<string>() == "https://store.example.com/terms");
        Assert.Contains(first["seller"]["links"].AsArray(), link => link["type"].GetValue<string>() == "privacy_policy");

        var second = variants[1].AsObject();

        Assert.False(second["availability"]["available"].GetValue<bool>());
        Assert.Equal("out_of_stock", second["availability"]["status"].GetValue<string>());
        Assert.False(second.ContainsKey("list_price"));
    }

    [Fact]
    public void APrice_IsInWholeMinorUnits_ForEveryCurrencysPrecision()
    {
        // Act & Assert
        Assert.Equal(1500, AcpFeedProductMapper.Price(1500m, "jpy")["amount"].GetValue<long>());
        Assert.Equal("JPY", AcpFeedProductMapper.Price(1500m, "jpy")["currency"].GetValue<string>());
        Assert.Equal(12345, AcpFeedProductMapper.Price(12.345m, "KWD")["amount"].GetValue<long>());
    }

    [Fact]
    public void ASaleToCome_IsNotThePriceYet()
    {
        // Arrange
        var item = OpenAIProductFeedWriterTests.Variant();

        item.SaleStartsUtc = _now.AddDays(1);

        // Act
        var variant = AcpFeedProductMapper.ToProducts([item], _now)[0]["variants"][0];

        // Assert
        Assert.Equal(7999, variant["price"]["amount"].GetValue<long>());
        Assert.Null(variant["list_price"]);
    }

    [Fact]
    public async Task ARequest_CarriesTheBearerKeyTheApiVersionAndTheIdempotencyKey()
    {
        // Arrange
        var body = AcpFeedApiClient.CreateUpsertBody(AcpFeedProductMapper.ToProducts([OpenAIProductFeedWriterTests.Simple()], _now));

        // Act
        using var request = AcpFeedApiClient.CreateRequest(HttpMethod.Patch, AcpFeedApiClient.Combine("https://api.example.com/acp/", "feeds/feed_1/products"), "secret", "key-1", body);

        // Assert
        Assert.Equal("https://api.example.com/acp/feeds/feed_1/products", request.RequestUri.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
        Assert.Equal("secret", request.Headers.Authorization.Parameter);
        Assert.Equal("2026-04-17", Assert.Single(request.Headers.GetValues("API-Version")));
        Assert.Equal("key-1", Assert.Single(request.Headers.GetValues("Idempotency-Key")));

        var sent = JsonNode.Parse(await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        Assert.Equal("PRODUCT2", sent["products"][0]["id"].GetValue<string>());
        Assert.Equal("HAT-1", sent["products"][0]["variants"][0]["id"].GetValue<string>());
    }

    [Fact]
    public void AnIdempotencyKey_IsTheSameForTheSameCall_AndDiffersForAnother()
    {
        // Act
        var first = AcpFeedApiClient.CreateIdempotencyKey("upsert", "target", "feed", "{}");
        var retry = AcpFeedApiClient.CreateIdempotencyKey("upsert", "target", "feed", "{}");
        var other = AcpFeedApiClient.CreateIdempotencyKey("upsert", "target", "feed", "{\"products\":[]}");

        // Assert
        Assert.Equal(first, retry);
        Assert.NotEqual(first, other);
    }
}
