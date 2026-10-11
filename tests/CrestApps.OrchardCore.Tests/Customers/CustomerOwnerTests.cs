using CrestApps.OrchardCore.Customers.Models;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Customers;

public sealed class CustomerOwnerTests
{
    [Fact]
    public void ForUser_SetsAuthenticatedKindAndId()
    {
        // Act
        var owner = CustomerOwner.ForUser("user-1");

        // Assert
        Assert.Equal(CustomerOwnerKind.Authenticated, owner.Kind);
        Assert.Equal("user-1", owner.Id);
    }

    [Fact]
    public void ForGuest_SetsGuestKindAndId()
    {
        // Act
        var owner = CustomerOwner.ForGuest("guest-1");

        // Assert
        Assert.Equal(CustomerOwnerKind.Guest, owner.Kind);
        Assert.Equal("guest-1", owner.Id);
    }

    [Fact]
    public void AuthenticatedKind_DefaultsToZero()
        => Assert.Equal(0, (int)CustomerOwnerKind.Authenticated);

    [Fact]
    public void Equals_WhenSameKindAndId_ReturnsTrue()
    {
        // Arrange
        var first = CustomerOwner.ForUser("user-1");
        var second = CustomerOwner.ForUser("user-1");

        // Assert
        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Equals_WhenDifferentKindButSameId_ReturnsFalse()
    {
        // Arrange
        var user = CustomerOwner.ForUser("same-id");
        var guest = CustomerOwner.ForGuest("same-id");

        // Assert
        Assert.NotEqual(user, guest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForUser_WhenIdIsNullOrEmpty_Throws(string id)
        => Assert.ThrowsAny<ArgumentException>(() => CustomerOwner.ForUser(id));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ForGuest_WhenIdIsNullOrEmpty_Throws(string id)
        => Assert.ThrowsAny<ArgumentException>(() => CustomerOwner.ForGuest(id));
}
