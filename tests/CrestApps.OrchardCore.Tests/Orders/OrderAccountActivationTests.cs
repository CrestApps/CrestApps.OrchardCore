using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Moq;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// SEC-C17h (review): the link sent to the order's address is where the account gets its password, so only the owner of
/// the mailbox ever has one. Following it proves the address and brings the order into the account.
/// </summary>
public sealed class OrderAccountActivationTests
{
    [Fact]
    public async Task FollowingTheLink_SetsThePassword_ProvesTheAddress_AndBringsTheOrderIn()
    {
        // Arrange: the account the order's confirmation made.
        var harness = new OrderAccountHarness();
        await harness.CreateController().Create(new OrderCreateAccountViewModel { Token = harness.Token }, returnUrl: null);

        var user = Assert.IsType<User>(harness.Created);
        user.UserId = "u1";

        harness.Users.Setup(manager => manager.FindByIdAsync("u1")).ReturnsAsync(user);
        harness.Users.Setup(manager => manager.ConfirmEmailAsync(user, "code-1"))
            .Callback(() => user.EmailConfirmed = true)
            .ReturnsAsync(IdentityResult.Success);
        harness.Users.Setup(manager => manager.AddPasswordAsync(user, "Password1!")).ReturnsAsync(IdentityResult.Success);

        // Act
        await harness.CreateController().Activate(new OrderAccountActivateViewModel { UserId = "u1", Code = "code-1", Password = "Password1!", ConfirmPassword = "Password1!" });

        // Assert
        harness.Users.Verify(manager => manager.AddPasswordAsync(user, "Password1!"), Times.Once());
        Assert.True(UserEmailOwnership.IsVerified(user.Properties, "pat@example.test"));
        Assert.Null(user.Properties[OrderAccountController.PendingOrderPropertyName]);
        Assert.Equal("u1", harness.Order.UserId);
    }

    [Fact]
    public async Task ALinkThatDoesntConfirm_SetsNoPassword()
    {
        // Arrange
        var harness = new OrderAccountHarness();
        await harness.CreateController().Create(new OrderCreateAccountViewModel { Token = harness.Token }, returnUrl: null);

        var user = Assert.IsType<User>(harness.Created);
        user.UserId = "u1";

        harness.Users.Setup(manager => manager.FindByIdAsync("u1")).ReturnsAsync(user);
        harness.Users.Setup(manager => manager.ConfirmEmailAsync(user, It.IsAny<string>())).ReturnsAsync(IdentityResult.Failed());

        // Act
        await harness.CreateController().Activate(new OrderAccountActivateViewModel { UserId = "u1", Code = "forged", Password = "Password1!", ConfirmPassword = "Password1!" });

        // Assert
        harness.Users.Verify(manager => manager.AddPasswordAsync(It.IsAny<IUser>(), It.IsAny<string>()), Times.Never());
        Assert.Null(harness.Order.UserId);
    }
}
