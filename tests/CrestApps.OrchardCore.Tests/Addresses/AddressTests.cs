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
            Name = "Jane Doe",
            Company = "Acme Inc",
            AddressLine1 = "1 Market St",
            AddressLine2 = "Suite 200",
            Country = "US",
            Region = "CA",
            County = "Santa Clara",
            City = "San Jose",
            District = "SPD",
            PostalCode = "95113",
            Phone = "+1-408-555-0100",
        };

        // Act
        var clone = address.Clone();

        // Assert
        Assert.NotSame(address, clone);
        Assert.Equal(address.Name, clone.Name);
        Assert.Equal(address.Company, clone.Company);
        Assert.Equal(address.AddressLine1, clone.AddressLine1);
        Assert.Equal(address.AddressLine2, clone.AddressLine2);
        Assert.Equal(address.Country, clone.Country);
        Assert.Equal(address.Region, clone.Region);
        Assert.Equal(address.County, clone.County);
        Assert.Equal(address.City, clone.City);
        Assert.Equal(address.District, clone.District);
        Assert.Equal(address.PostalCode, clone.PostalCode);
        Assert.Equal(address.Phone, clone.Phone);
    }

    [Fact]
    public void Clone_ReturnsIndependentImmutableSnapshot()
    {
        // Arrange
        var address = new Address
        {
            Country = "CA",
            Region = "ON",
        };

        // Act
        var clone = address.Clone();

        // A modified copy must be produced through a new snapshot; the address itself is init-only.
        var modified = new Address
        {
            Country = "US",
            Region = clone.Region,
        };

        // Assert
        Assert.NotSame(address, clone);
        Assert.Equal("CA", clone.Country);
        Assert.Equal("ON", clone.Region);
        Assert.Equal("CA", address.Country);
        Assert.Equal("US", modified.Country);
        Assert.Equal("ON", modified.Region);
    }
}
