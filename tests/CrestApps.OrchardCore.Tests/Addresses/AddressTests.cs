using CrestApps.OrchardCore.Addresses.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Addresses;

public sealed class AddressTests
{
    [Fact]
    public void Clone_CopiesAllComponents()
    {
        // Arrange
        var address = new Address
        {
            Country = "US",
            Region = "CA",
            County = "Santa Clara",
            City = "San Jose",
            District = "SPD",
            PostalCode = "95113",
        };

        // Act
        var clone = address.Clone();

        // Assert
        Assert.NotSame(address, clone);
        Assert.Equal(address.Country, clone.Country);
        Assert.Equal(address.Region, clone.Region);
        Assert.Equal(address.County, clone.County);
        Assert.Equal(address.City, clone.City);
        Assert.Equal(address.District, clone.District);
        Assert.Equal(address.PostalCode, clone.PostalCode);
    }

    [Fact]
    public void Clone_DoesNotMutateOriginal_WhenCloneChanges()
    {
        // Arrange
        var address = new Address
        {
            Country = "CA",
            Region = "ON",
        };

        // Act
        var clone = address.Clone();
        clone.Country = "US";
        clone.Region = "NY";

        // Assert
        Assert.Equal("CA", address.Country);
        Assert.Equal("ON", address.Region);
    }
}
