using System.Collections.Generic;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Addresses;

public sealed class DefaultAddressResolverTests
{
    [Fact]
    public void BuildAddress_UsesAreaCode_WhenPresent()
    {
        // Arrange
        var country = CreateArea("country-id", "United States", "US");
        var region = CreateArea("region-id", "California", "CA");

        var addressPart = new JsonObject
        {
            ["Country"] = Selector("country-id"),
            ["Region"] = Selector("region-id"),
        };

        var resolved = new Dictionary<string, GeographicAreaIndex>
        {
            ["country-id"] = country,
            ["region-id"] = region,
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, resolved);

        // Assert
        Assert.Equal("US", address.Country);
        Assert.Equal("CA", address.Region);
    }

    [Fact]
    public void BuildAddress_FallsBackToDisplayText_WhenCodeMissing()
    {
        // Arrange
        var county = CreateArea("county-id", "Santa Clara", code: null);
        var city = CreateArea("city-id", "San Jose", code: null);

        var addressPart = new JsonObject
        {
            ["County"] = Selector("county-id"),
            ["City"] = Selector("city-id"),
        };

        var resolved = new Dictionary<string, GeographicAreaIndex>
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
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, GeographicAreaIndex>());

        // Assert
        Assert.Equal("95113", address.PostalCode);
    }

    [Fact]
    public void BuildAddress_CopiesRecipientAndStreetContactFields()
    {
        // Arrange
        var addressPart = new JsonObject
        {
            ["Name"] = new JsonObject { ["Text"] = " Jane Doe " },
            ["Company"] = new JsonObject { ["Text"] = "Acme Inc" },
            ["AddressLine1"] = new JsonObject { ["Text"] = "1 Market St" },
            ["AddressLine2"] = new JsonObject { ["Text"] = "Suite 200" },
            ["Phone"] = new JsonObject { ["Text"] = "+1-408-555-0100" },
        };

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, GeographicAreaIndex>());

        // Assert
        Assert.Equal("Jane Doe", address.Name);
        Assert.Equal("Acme Inc", address.Company);
        Assert.Equal("1 Market St", address.AddressLine1);
        Assert.Equal("Suite 200", address.AddressLine2);
        Assert.Equal("+1-408-555-0100", address.Phone);
    }

    [Fact]
    public void BuildAddress_ReturnsNullComponents_WhenSelectorsEmpty()
    {
        // Arrange
        var addressPart = new JsonObject();

        // Act
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, GeographicAreaIndex>());

        // Assert
        Assert.Null(address.Name);
        Assert.Null(address.Company);
        Assert.Null(address.AddressLine1);
        Assert.Null(address.AddressLine2);
        Assert.Null(address.Country);
        Assert.Null(address.Region);
        Assert.Null(address.County);
        Assert.Null(address.City);
        Assert.Null(address.District);
        Assert.Null(address.PostalCode);
        Assert.Null(address.Phone);
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
        var address = DefaultAddressResolver.BuildAddress(addressPart, new Dictionary<string, GeographicAreaIndex>());

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

    private static GeographicAreaIndex CreateArea(string contentItemId, string displayText, string code)
    {
        return new GeographicAreaIndex
        {
            ContentItemId = contentItemId,
            DisplayText = displayText,
            Code = code,
            Published = true,
            Latest = true,
        };
    }
}
