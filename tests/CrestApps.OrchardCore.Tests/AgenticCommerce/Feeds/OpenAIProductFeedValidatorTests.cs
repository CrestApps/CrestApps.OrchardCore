using CrestApps.OrchardCore.AgenticCommerce.Feeds;
using CrestApps.OrchardCore.Products.Core.Feeds;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce.Feeds;

/// <summary>
/// An item that breaks one of OpenAI's field rules is left out of the OpenAI feed, with why.
/// </summary>
public sealed class OpenAIProductFeedValidatorTests
{
    private static readonly OpenAIProductFeedContext _context = new() { StoreCountry = "US", TargetCountries = ["US"] };

    [Fact]
    public void AWellFormedSimpleProductAndVariant_PassEveryRule()
    {
        // Act & Assert
        Assert.Empty(OpenAIProductFeedValidator.Validate(Record(OpenAIProductFeedWriterTests.Simple())));
        Assert.Empty(OpenAIProductFeedValidator.Validate(Record(OpenAIProductFeedWriterTests.Variant())));
    }

    [Fact]
    public void ARecordMissingWhatOpenAIRequires_IsNamedForEachField()
    {
        // Arrange
        var record = new OpenAIProductFeedRecord
        {
            ItemId = "X",
            Availability = "sold",
            Price = "79,99 usd",
        };

        // Act
        var problems = OpenAIProductFeedValidator.Validate(record);

        // Assert
        Assert.Contains(problems, problem => problem.StartsWith("title", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("description", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("brand", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("seller_name", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("url", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("image_url", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("availability", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.StartsWith("price", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("79.99 USD", "79.99 USD")]
    [InlineData("79.99 USD", "89.00 USD")]
    [InlineData("79.99 USD", "50.00 EUR")]
    public void ASalePrice_HasToBeBelowThePriceInTheSameCurrency(string price, string sale)
    {
        // Arrange
        var record = Record(OpenAIProductFeedWriterTests.Simple());

        record.Price = price;
        record.SalePrice = sale;

        // Act & Assert
        Assert.Contains(OpenAIProductFeedValidator.Validate(record), problem => problem.StartsWith("sale_price", StringComparison.Ordinal));
    }

    [Fact]
    public void AGtin_NeedsItsCheckDigit()
    {
        // Arrange
        var record = Record(OpenAIProductFeedWriterTests.Simple());

        record.Gtin = "4006381333932";

        // Act & Assert
        Assert.Contains(OpenAIProductFeedValidator.Validate(record), problem => problem.StartsWith("gtin", StringComparison.Ordinal));
    }

    [Fact]
    public void AVariant_NeedsAGroupOtherThanItself_AndItsOptions()
    {
        // Arrange
        var record = Record(OpenAIProductFeedWriterTests.Variant());

        record.GroupId = record.ItemId;
        record.VariantDict = null;

        // Act
        var problems = OpenAIProductFeedValidator.Validate(record);

        // Assert
        Assert.Contains(problems, problem => problem.Contains("group_id", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("variant_dict", StringComparison.Ordinal));
    }

    [Fact]
    public void AnItemAgentsMayCheckOut_NeedsThePrivacyPolicyAndTerms()
    {
        // Arrange
        var item = OpenAIProductFeedWriterTests.Variant();

        item.PrivacyPolicyUrl = null;
        item.TermsOfServiceUrl = null;

        // Act
        var problems = OpenAIProductFeedValidator.Validate(Record(item));

        // Assert
        Assert.Contains(problems, problem => problem.Contains("seller_privacy_policy", StringComparison.Ordinal));
        Assert.Contains(problems, problem => problem.Contains("seller_tos", StringComparison.Ordinal));

        item.AgentCheckout = false;

        Assert.Empty(OpenAIProductFeedValidator.Validate(Record(item)));
    }

    [Fact]
    public void TheFeed_LeavesOutWhatOpenAIWouldReject_AndSaysWhy()
    {
        // Arrange
        var broken = OpenAIProductFeedWriterTests.Simple();

        broken.Id = "BROKEN";
        broken.Brand = null;

        var feed = new ProductFeed(
            [OpenAIProductFeedWriterTests.Simple(), broken],
            [new ProductFeedProblem("NO-PHOTO", "Scarf", "image_link is missing: give the product a photo", LeftOut: true)],
            new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var built = OpenAIProductFeedService.Build(feed, _context);

        // Assert
        Assert.Equal("HAT-1", Assert.Single(built.Records).ItemId);
        Assert.Contains(built.Problems, problem => problem.ItemId == "BROKEN" && problem.Problem.StartsWith("brand", StringComparison.Ordinal));
        Assert.Contains(built.Problems, problem => problem.ItemId == "NO-PHOTO");
        Assert.Equal(feed.BuiltUtc, built.BuiltUtc);
    }

    [Theory]
    [InlineData("79.99 USD", true)]
    [InlineData("1500 JPY", true)]
    [InlineData("1,079.99 USD", false)]
    [InlineData("79.99 usd", false)]
    [InlineData("79.99USD", false)]
    public void Money_IsAnAmountASpaceAndAnUppercaseCode(string value, bool valid)
        => Assert.Equal(valid, OpenAIProductFeedValidator.ParseMoney(value) is not null);

    private static OpenAIProductFeedRecord Record(ProductFeedItem item)
        => OpenAIProductFeedWriter.ToRecord(item, _context);
}
