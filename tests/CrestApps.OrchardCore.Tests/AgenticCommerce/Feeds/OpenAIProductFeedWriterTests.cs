using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce.Feeds;

/// <summary>
/// The OpenAI product feed carries OpenAI's fields for a simple product and for each variant of one, as JSON lines or
/// CSV, gzipped or not, in shards with stable names.
/// </summary>
public sealed class OpenAIProductFeedWriterTests
{
    private static readonly OpenAIProductFeedContext _context = new()
    {
        StoreCountry = "us",
        TargetCountries = ["US", "CA"],
    };

    [Fact]
    public void AVariant_HasItsGroupAndOptions_AndOpenAIsMoneyAndAvailability()
    {
        // Arrange
        var item = Variant();

        // Act
        var record = OpenAIProductFeedWriter.ToRecord(item, _context);

        // Assert
        Assert.Equal("SHOE-BLK-10", record.ItemId);
        Assert.Equal("PRODUCT1", record.GroupId);
        Assert.True(record.ListingHasVariations);
        Assert.Equal("Black", record.VariantDict["Color"]);
        Assert.Equal("10", record.VariantDict["Size"]);
        Assert.Equal("79.99 USD", record.Price);
        Assert.Equal("59.50 USD", record.SalePrice);
        Assert.Equal("pre_order", record.Availability);
        Assert.Equal("4.99 USD", record.ShippingPrice);
        Assert.Equal(1.2m, record.Weight);
        Assert.Equal("kg", record.ItemWeightUnit);
        Assert.Equal("cm", record.DimensionsUnit);
        Assert.True(record.AcceptsReturns);
        Assert.Equal(30, record.ReturnDeadlineInDays);
        Assert.Equal("https://store.example.com/privacy", record.SellerPrivacyPolicy);
        Assert.Equal("https://store.example.com/terms", record.SellerTos);
        Assert.True(record.IsEligibleSearch);
        Assert.True(record.IsEligibleCheckout);
        Assert.Equal("US", record.StoreCountry);
        Assert.Equal(["US", "CA"], record.TargetCountries);
        Assert.Equal("Apparel > Shoes", record.ProductCategory);
    }

    [Fact]
    public void ASimpleProduct_HasNoGroup_AndItsJsonLineLeavesOutWhatIsEmpty()
    {
        // Arrange
        var item = Simple();

        // Act
        var record = OpenAIProductFeedWriter.ToRecord(item, _context);
        var line = JsonNode.Parse(OpenAIProductFeedWriter.ToJsonLine(record)).AsObject();

        // Assert
        Assert.Null(record.GroupId);
        Assert.False(record.ListingHasVariations);
        Assert.Null(record.VariantDict);
        Assert.Equal("HAT-1", line["item_id"].GetValue<string>());
        Assert.Equal("1500 JPY", line["price"].GetValue<string>());
        Assert.Equal("in_stock", line["availability"].GetValue<string>());
        Assert.False(line["listing_has_variations"].GetValue<bool>());
        Assert.False(line["is_eligible_checkout"].GetValue<bool>());
        Assert.False(line.ContainsKey("group_id"));
        Assert.False(line.ContainsKey("sale_price"));
        Assert.False(line.ContainsKey("weight"));
        Assert.False(line.ContainsKey("gtin"));
    }

    [Fact]
    public void AnItemAgentsMayNotShow_IsSentAsNotEligible_SoOpenAIDropsItSooner()
    {
        // Arrange
        var item = Simple();

        item.ShowToAgents = false;
        item.AgentCheckout = true;

        // Act
        var record = OpenAIProductFeedWriter.ToRecord(item, _context);

        // Assert
        Assert.False(record.IsEligibleSearch);
        Assert.False(record.IsEligibleCheckout);
    }

    [Fact]
    public void TheCsv_HasOpenAIsColumns_AndEscapesCommasQuotesAndOptions()
    {
        // Arrange
        var item = Variant();

        item.Title = "Runner, \"Black\"";

        var record = OpenAIProductFeedWriter.ToRecord(item, _context);

        // Act
        var csv = Encoding.UTF8.GetString(OpenAIProductFeedWriter.ToBytes([record], OpenAIProductFeedFormats.Csv));
        var lines = csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        // Assert
        Assert.Equal(2, lines.Length);
        Assert.StartsWith("item_id,title,description,url,brand,seller_name,image_url,additional_image_urls,availability,price,", lines[0], StringComparison.Ordinal);
        Assert.Equal(OpenAIProductFeedWriter.Columns.Count, lines[0].Split(',').Length);
        Assert.StartsWith("SHOE-BLK-10,\"Runner, \"\"Black\"\"\",", lines[1], StringComparison.Ordinal);
        Assert.Contains(",\"{\"\"Color\"\":\"\"Black\"\",\"\"Size\"\":\"\"10\"\"}\",", lines[1], StringComparison.Ordinal);
        Assert.Contains(",\"https://store.example.com/media/shoe-2.jpg,https://store.example.com/media/shoe-3.jpg\",", lines[1], StringComparison.Ordinal);
        Assert.Contains(",true,true,US,\"US,CA\"", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void AGzippedFeed_UnzipsToTheSameJsonLines()
    {
        // Arrange
        var records = new[] { OpenAIProductFeedWriter.ToRecord(Simple(), _context), OpenAIProductFeedWriter.ToRecord(Variant(), _context) };

        // Act
        var gzipped = OpenAIProductFeedWriter.ToBytes(records, OpenAIProductFeedFormats.JsonLinesGzip);

        using var input = new GZipStream(new MemoryStream(gzipped), CompressionMode.Decompress);
        using var reader = new StreamReader(input, Encoding.UTF8);
        var text = reader.ReadToEnd();

        // Assert
        Assert.Equal(Encoding.UTF8.GetString(OpenAIProductFeedWriter.ToBytes(records, OpenAIProductFeedFormats.JsonLines)), text);
        Assert.Equal(2, text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void Shards_HaveStableNames_AndHoldTheItemsInIdOrder()
    {
        // Arrange
        var records = new[] { "C", "A", "E", "B", "D" }.Select(id => new OpenAIProductFeedRecord { ItemId = id }).ToList();

        // Act
        var shards = OpenAIProductFeedWriter.Shard(records, OpenAIProductFeedFormats.JsonLinesGzip, maxItemsPerShard: 2);
        var empty = OpenAIProductFeedWriter.Shard([], OpenAIProductFeedFormats.CsvGzip);

        // Assert
        Assert.Equal(["products-00001.jsonl.gz", "products-00002.jsonl.gz", "products-00003.jsonl.gz"], shards.Select(shard => shard.FileName));
        Assert.Equal(["A", "B"], shards[0].Records.Select(record => record.ItemId));
        Assert.Equal(["E"], shards[2].Records.Select(record => record.ItemId));
        Assert.Equal("products-00001.csv.gz", Assert.Single(empty).FileName);
    }

    internal static ProductFeedItem Variant()
        => new()
        {
            Id = "SHOE-BLK-10",
            ItemGroupId = "PRODUCT1",
            ContentItemId = "PRODUCT1",
            VariantId = "V1",
            Title = "Runner - Black / 10",
            ProductTitle = "Runner",
            Description = "A light running shoe.",
            Link = "https://store.example.com/runner",
            ImageLink = "https://store.example.com/media/shoe.jpg",
            AdditionalImageLinks = ["https://store.example.com/media/shoe-2.jpg", "https://store.example.com/media/shoe-3.jpg"],
            Availability = "preorder",
            AvailableQuantity = 0,
            Price = 79.99m,
            SalePrice = 59.5m,
            Currency = "USD",
            Brand = "Contoso",
            Gtin = "4006381333931",
            Condition = "new",
            ProductType = "Apparel > Shoes",
            Color = "Black",
            Size = "10",
            VariantOptions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Color"] = "Black",
                ["Size"] = "10",
            },
            SellerName = "Contoso",
            SellerUrl = "https://store.example.com/",
            TermsOfServiceUrl = "https://store.example.com/terms",
            PrivacyPolicyUrl = "https://store.example.com/privacy",
            ReturnPolicyUrl = "https://store.example.com/returns",
            ShippingPrice = 4.99m,
            AcceptsReturns = true,
            ReturnWindowDays = 30,
            AcceptsExchanges = true,
            Weight = 1.2m,
            WeightUnit = "kg",
            Length = 32m,
            Width = 20m,
            Height = 12m,
            DimensionsUnit = "cm",
            ShowToAgents = true,
            AgentCheckout = true,
        };

    internal static ProductFeedItem Simple()
        => new()
        {
            Id = "HAT-1",
            ContentItemId = "PRODUCT2",
            Title = "Hat",
            ProductTitle = "Hat",
            Description = "A warm hat.",
            Link = "https://store.example.com/hat",
            ImageLink = "https://store.example.com/media/hat.jpg",
            Availability = "in_stock",
            Price = 1500m,
            Currency = "JPY",
            Brand = "Contoso",
            SellerName = "Contoso",
            ShowToAgents = true,
        };
}
