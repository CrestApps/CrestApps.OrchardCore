using System.Xml.Linq;
using CrestApps.OrchardCore.Products.Feeds;

namespace CrestApps.OrchardCore.Tests.Products;

/// <summary>
/// A product feed item meets Google Merchant Center's rules, and is written as its RSS attributes or as CSV.
/// </summary>
public sealed class ProductFeedTests
{
    private static readonly XNamespace _g = "http://base.google.com/ns/1.0";

    [Theory]
    [InlineData("4006381333931", true)]
    [InlineData("4006381333932", false)]
    [InlineData("036000291452", true)]
    [InlineData("96385074", true)]
    [InlineData("12345", false)]
    [InlineData("40063813339AB", false)]
    public void AGtin_HasEightTwelveThirteenOrFourteenDigits_AndItsCheckDigit(string gtin, bool valid)
        => Assert.Equal(valid, ProductFeedValidator.IsValidGtin(gtin));

    [Fact]
    public void AnItem_MissingWhatMerchantCenterNeeds_IsNamedForEachProblem()
    {
        var item = new ProductFeedItem
        {
            Id = "SKU-1",
            Title = "Hat",
            Description = "A hat.",
            Link = "/relative",
            Availability = "sold",
            Price = 0m,
            Currency = "USD",
            SalePrice = 5m,
            Condition = "mint",
            Gtin = "123",
        };

        var problems = ProductFeedValidator.Validate(item);

        Assert.Contains(problems, problem => problem.StartsWith("link", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("image_link", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("availability", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("price", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("sale_price", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("condition", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("gtin", StringComparison.Ordinal));
        Assert.Empty(ProductFeedValidator.Validate(Valid()));
    }

    [Fact]
    public void TheFeed_IsGoogleRss_WithPricesInTheirCurrency_AndNoIdentifierFlagged()
    {
        var item = Valid();
        item.SalePrice = 15m;
        item.SaleStartsUtc = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        item.SaleEndsUtc = new DateTime(2026, 11, 8, 0, 0, 0, DateTimeKind.Utc);
        item.AdditionalImageLinks = ["https://store.example.com/media/hat-2.jpg"];

        var xml = XDocument.Parse(ProductFeedWriter.ToXml(new ProductFeed([item, Valid("SKU-2", brand: "Contoso", gtin: "4006381333931")], [], DateTime.UtcNow), "Contoso", "https://store.example.com"));
        var items = xml.Root.Element("channel").Elements("item").ToList();

        Assert.Equal("2.0", xml.Root.Attribute("version").Value);
        Assert.Equal("20.00 USD", items[0].Element(_g + "price").Value);
        Assert.Equal("15.00 USD", items[0].Element(_g + "sale_price").Value);
        Assert.Equal("2026-11-01T00:00Z/2026-11-08T00:00Z", items[0].Element(_g + "sale_price_effective_date").Value);
        Assert.Equal("no", items[0].Element(_g + "identifier_exists").Value);
        Assert.Null(items[1].Element(_g + "identifier_exists"));
        Assert.Equal("4006381333931", items[1].Element(_g + "gtin").Value);

        var csv = ProductFeedWriter.ToCsv(new ProductFeed([item], [], DateTime.UtcNow)).Split('\n');

        Assert.StartsWith("id,item_group_id,title,", csv[0], StringComparison.Ordinal);
        Assert.Contains("\"15.00 USD\"", csv[1], StringComparison.Ordinal);
    }

    [Fact]
    public void ADescription_IsPlainText()
        => Assert.Equal("A warm hat & scarf.", ProductFeedValidator.ToPlainText("<p>A <strong>warm</strong>\n hat &amp; scarf.</p>"));

    private static ProductFeedItem Valid(string id = "SKU-1", string brand = null, string gtin = null)
        => new()
        {
            Id = id,
            Title = "Hat",
            Description = "A hat.",
            Link = "https://store.example.com/hat",
            ImageLink = "https://store.example.com/media/hat.jpg",
            Availability = "in_stock",
            Price = 20m,
            Currency = "USD",
            Brand = brand,
            Gtin = gtin,
        };
}
