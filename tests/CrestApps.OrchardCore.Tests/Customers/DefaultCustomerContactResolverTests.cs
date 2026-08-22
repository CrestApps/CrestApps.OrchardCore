using CrestApps.OrchardCore.Customers.Core.Services;
using CrestApps.OrchardCore.Customers.Models;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using Xunit;

namespace CrestApps.OrchardCore.Tests.Customers;

public sealed class DefaultCustomerContactResolverTests
{
    [Fact]
    public async Task ResolveAsync_WhenOwnerIsNull_ReturnsNull()
    {
        // Arrange
        var resolver = new DefaultCustomerContactResolver(new Mock<IUserService>(MockBehavior.Strict).Object);

        // Act
        var contact = await resolver.ResolveAsync(null, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(contact);
    }

    [Fact]
    public async Task ResolveAsync_WhenGuest_ReturnsCapturedGuestContact()
    {
        // Arrange
        var resolver = new DefaultCustomerContactResolver(new Mock<IUserService>(MockBehavior.Strict).Object);
        var guest = new CustomerContact { DisplayName = "Guest", Email = "guest@example.com" };

        // Act
        var contact = await resolver.ResolveAsync(CustomerOwner.ForGuest("guest-1"), guest, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(guest, contact);
    }

    [Fact]
    public async Task ResolveAsync_WhenAuthenticatedUserExists_ReturnsUserContact()
    {
        // Arrange
        var userService = new Mock<IUserService>();
        userService
            .Setup(s => s.GetUserByUniqueIdAsync("user-1"))
            .ReturnsAsync(new User { UserName = "jane", Email = "jane@example.com" });

        var resolver = new DefaultCustomerContactResolver(userService.Object);

        // Act
        var contact = await resolver.ResolveAsync(CustomerOwner.ForUser("user-1"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(contact);
        Assert.Equal("jane", contact.DisplayName);
        Assert.Equal("jane@example.com", contact.Email);
    }

    [Fact]
    public async Task ResolveAsync_WhenAuthenticatedUserMissing_ReturnsNull()
    {
        // Arrange
        var userService = new Mock<IUserService>();
        userService
            .Setup(s => s.GetUserByUniqueIdAsync(It.IsAny<string>()))
            .ReturnsAsync((IUser)null);

        var resolver = new DefaultCustomerContactResolver(userService.Object);

        // Act
        var contact = await resolver.ResolveAsync(CustomerOwner.ForUser("user-1"), null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(contact);
    }
}
