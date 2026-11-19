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
    [InlineData(FulfillmentReasons.Sent, "Sent. The partner's order is Tee.")]
    public void Reason_ToldInWordsWithItsValues(string reason, string expected)
    {
        // Arrange
        var text = new FulfillmentRequestText(RecipeRoundTrip.Localizer<FulfillmentRequestText>());

        // Act
        var sentence = text.Reason(new FulfillmentRequest { Reason = reason, ReasonArguments = ["Tee", "First, Second"] }).Value;

        // Assert
        Assert.StartsWith(expected, sentence, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the displayed sentences for stored status codes, including requests recorded with the old deletion reason.
    /// </summary>
    /// <param name="reason">The stored reason code.</param>
    /// <param name="status">The stored status argument, if present.</param>
    /// <param name="expected">The sentence displayed to the user.</param>
    [Theory]
    [InlineData(FulfillmentReasons.NotPaid, "Draft", "The order is still a draft.")]
    [InlineData(FulfillmentReasons.NotPaid, "AwaitingPayment", "The order is awaiting payment.")]
    [InlineData(FulfillmentReasons.NotPaid, "Canceled", "The order was canceled.")]
    [InlineData(FulfillmentReasons.NotPaid, "Deleted", "The order was deleted.")]
    [InlineData(FulfillmentReasons.NotPaid, null, "The order has not been paid.")]
    [InlineData(FulfillmentReasons.OrderDeleted, null, "The order was deleted.")]
    public void Reason_ExplainsTheOrderStatus(string reason, string status, string expected)
    {
        var text = new FulfillmentRequestText(RecipeRoundTrip.Localizer<FulfillmentRequestText>());
        var request = new FulfillmentRequest { Reason = reason };

        if (status is not null)
        {
            request.ReasonArguments.Add(status);
        }

        Assert.Equal(expected, text.Reason(request).Value);
    }
}
