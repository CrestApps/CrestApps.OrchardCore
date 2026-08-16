using System.Collections.Generic;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses;
using CrestApps.OrchardCore.Addresses.Services;
using OrchardCore.ContentManagement;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Addresses;

public sealed class DefaultAddressResolverTests
{
    [Fact]
    public void BuildAddress_UsesPartCode_WhenPresent()
    {
        // Arrange
        var country = CreateGeographic(AddressConstants.CountryPart, "United States", "us");
        var region = CreateGeographic(AddressConstants.RegionPart, "California", "ca");

        var addressPart = new JsonObject
        {
            ["Country"] = Selector("country-id"),
            ["Region"] = Selector("region-id"),
        };

        var resolved = new Dictionary<string, ContentItem>
        {
            ["country-id"] = country,
            ["region-id"] = region,
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, resolved);

        // Assert
        Assert.Equal("us", address.Country);
        Assert.Equal("ca", address.Region);
    }

    [Fact]
    public void BuildAddress_FallsBackToDisplayText_WhenCodeMissing()
    {
        // Arrange
        var county = CreateGeographic(AddressConstants.CountyPart, "Santa Clara", code: null);
        var city = CreateGeographic(AddressConstants.CityPart, "San Jose", code: null);

        var addressPart = new JsonObject
        {
            ["County"] = Selector("county-id"),
            ["City"] = Selector("city-id"),
        };

        var resolved = new Dictionary<string, ContentItem>
        {
            ["county-id"] = county,
            ["city-id"] = city,
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, resolved);

        // Assert
        Assert.Equal("Santa Clara", address.County);
        Assert.Equal("San Jose", address.City);
    }

    [Fact]
    public void BuildAddress_CopiesPostalCode_Verbatim()
    {
        // Arrange
        var addressPart = new JsonObject
        {
            ["PostalCode"] = new JsonObject { ["Text"] = " 95113 " },
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, ContentItem>());

        // Assert
        Assert.Equal("95113", address.PostalCode);
    }

    [Fact]
    public void BuildAddress_ReturnsNullComponents_WhenSelectorsEmpty()
    {
        // Arrange
        var addressPart = new JsonObject();

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, ContentItem>());

        // Assert
        Assert.Null(address.Country);
        Assert.Null(address.Region);
        Assert.Null(address.County);
        Assert.Null(address.City);
        Assert.Null(address.District);
        Assert.Null(address.PostalCode);
    }

    [Fact]
    public void BuildAddress_ReturnsNullComponent_WhenReferenceUnresolved()
    {
        // Arrange
        var addressPart = new JsonObject
        {
            ["District"] = Selector("missing-id"),
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, ContentItem>());

        // Assert
        Assert.Null(address.District);
    }

    private static JsonObject Selector(string contentItemId)
    {
        return new JsonObject
        {
            ["ContentItemIds"] = new JsonArray(contentItemId),
        };
    }

    private static ContentItem CreateGeographic(string partName, string displayText, string code)
    {
        var contentItem = new ContentItem
        {
            DisplayText = displayText,
        };

        var part = new JsonObject();

        if (!string.IsNullOrEmpty(code))
        {
            part[AddressConstants.CodeField] = new JsonObject { ["Text"] = code };
        }

        JsonNode content = contentItem.Content;
        content[partName] = part;

        return contentItem;
    }
}
