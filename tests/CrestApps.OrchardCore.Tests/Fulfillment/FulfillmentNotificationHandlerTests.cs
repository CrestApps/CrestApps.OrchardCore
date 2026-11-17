using CrestApps.OrchardCore.Fulfillment;
using CrestApps.OrchardCore.Fulfillment.Core.Models;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Shipping;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Notifications;
using OrchardCore.Notifications.Models;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;
using OrchardCore.Security.Services;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// A request that needs attention notifies each person who may ship orders once, with a link to the order, and the
/// stored reason code is told in words.
/// </summary>
public sealed class FulfillmentNotificationHandlerTests
{
    [Fact]
    public async Task NeedsAttentionAsync_NotifiesEachPersonWhoShipsOrdersOnce()
    {
        // Arrange
        var sent = new List<(string UserId, NotificationMessage Message)>();
        var notifications = new Mock<INotificationService>();

        notifications.Setup(service => service.SendAsync(It.IsAny<object>(), It.IsAny<INotificationMessage>(), It.IsAny<CancellationToken>()))
            .Callback((object user, INotificationMessage message, CancellationToken _) => sent.Add((((User)user).UserId, (NotificationMessage)message)))
            .ReturnsAsync(new NotificationSendResult());

        var admin = new User { UserId = "admin-id", UserName = "admin" };
        var shipper = new User { UserId = "shipper-id", UserName = "shipper" };
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        userManager.Setup(manager => manager.GetUsersInRoleAsync("Administrator")).ReturnsAsync([admin]);
        userManager.Setup(manager => manager.GetUsersInRoleAsync("Shipper")).ReturnsAsync([shipper, admin]);
        userManager.Setup(manager => manager.GetUsersInRoleAsync("Editor")).ReturnsAsync([new User { UserId = "editor-id" }]);
        userManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((User)user).UserId);

        var roleService = new Mock<IRoleService>();

        roleService.Setup(service => service.GetRolesAsync()).ReturnsAsync(
        [
            new Role { RoleName = "Shipper", RoleClaims = [new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = ShippingPermissions.FulfillOrders.Name }] },
            new Role { RoleName = "Editor", RoleClaims = [new RoleClaim { ClaimType = Permission.ClaimType, ClaimValue = "EditContent" }] },
        ]);

        var services = new ServiceCollection()
            .AddSingleton(notifications.Object)
            .AddSingleton(userManager.Object)
            .AddSingleton(roleService.Object)
            .BuildServiceProvider();
        var handler = new FulfillmentNotificationHandler(
            services,
            new FulfillmentRequestText(RecipeRoundTrip.Localizer<FulfillmentRequestText>()),
            [],
            RecipeRoundTrip.Localizer<FulfillmentNotificationHandler>());
        var request = new FulfillmentRequest
        {
            ItemId = "request-1",
            OrderId = "order-1",
            OrderNumber = "261117-ABC",
            ProviderName = "Printify",
            Status = FulfillmentRequestStatus.NeedsAttention,
            Reason = FulfillmentReasons.Unclear,
        };

        // Act
        await handler.NeedsAttentionAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["admin-id", "shipper-id"], sent.Select(entry => entry.UserId).Order());
        Assert.All(sent, entry =>
        {
            Assert.Equal(FulfillmentConstants.NeedsAttentionNotificationType, entry.Message.Type);
            Assert.Equal("request-1", entry.Message.ReferenceId);
            Assert.Equal("~/Admin/orders/order-1", entry.Message.Url);
            Assert.Contains("261117-ABC", entry.Message.Subject, StringComparison.Ordinal);
            Assert.Contains("did not answer clearly", entry.Message.TextBody, StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task NeedsAttentionAsync_NotificationsOff_DoesNothing()
    {
        // Arrange
        var handler = new FulfillmentNotificationHandler(
            new ServiceCollection().BuildServiceProvider(),
            new FulfillmentRequestText(RecipeRoundTrip.Localizer<FulfillmentRequestText>()),
            [],
            RecipeRoundTrip.Localizer<FulfillmentNotificationHandler>());

        // Act
        var exception = await Record.ExceptionAsync(() => handler.NeedsAttentionAsync(new FulfillmentRequest { ItemId = "request-1" }, TestContext.Current.CancellationToken));

        // Assert
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(FulfillmentReasons.ConflictingClaims, "\"Tee\" is made by more than one partner (First, Second).")]
    [InlineData(FulfillmentReasons.NotPaid, "The order is not paid (it is Canceled).")]
    [InlineData(FulfillmentReasons.Sent, "Sent. The partner's order is Tee.")]
    public void Reason_ToldInWordsWithItsValues(string reason, string expected)
    {
        // Arrange
        var text = new FulfillmentRequestText(RecipeRoundTrip.Localizer<FulfillmentRequestText>());
        var arguments = reason == FulfillmentReasons.NotPaid ? new List<string> { "Canceled" } : ["Tee", "First, Second"];

        // Act
        var sentence = text.Reason(new FulfillmentRequest { Reason = reason, ReasonArguments = arguments }).Value;

        // Assert
        Assert.StartsWith(expected, sentence, StringComparison.Ordinal);
    }
}
