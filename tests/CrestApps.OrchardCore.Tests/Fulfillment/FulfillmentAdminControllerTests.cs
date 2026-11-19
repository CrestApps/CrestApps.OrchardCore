using System.Security.Claims;
using CrestApps.OrchardCore.Fulfillment.Controllers;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Fulfillment;

/// <summary>
/// Verifies the notifications shown when an operator sends an order from its admin page.
/// </summary>
public sealed class FulfillmentAdminControllerTests
{
    /// <summary>
    /// Verifies that sending an order with no requests reports whether another worker holds the lock or no partner is connected.
    /// </summary>
    /// <param name="lockAvailable">Whether the manual send can take the order's lock.</param>
    /// <param name="type">The notification type.</param>
    /// <param name="message">The message shown to the shipper.</param>
    [Theory]
    [InlineData(false, NotifyType.Information, "Someone else is sending this order. Refresh in a moment.")]
    [InlineData(true, NotifyType.Warning, "No fulfillment partner is connected.")]
    public async Task Send_WithNoRequests_ReportsWhy(bool lockAvailable, NotifyType type, string message)
    {
        // Arrange
        await using var harness = new FulfillmentTestHarness { LockAvailable = lockAvailable };
        await harness.InitializeAsync();
        await using var session = harness.Store.CreateSession();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());
        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(harness.Order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(harness.Order);
        var notifier = new Mock<INotifier>();
        var html = new Mock<IHtmlLocalizer<FulfillmentAdminController>>();
        html.Setup(localizer => localizer[It.IsAny<string>()]).Returns((string key) => new LocalizedHtmlString(key, key));

        var controller = new FulfillmentAdminController(authorization.Object, orders.Object, null, harness.Sender(session), null, [], notifier.Object, html.Object, null)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
            Url = Mock.Of<IUrlHelper>(),
        };

        // Act
        var result = await controller.Send(harness.Order.ItemId, null);

        // Assert
        Assert.IsType<RedirectToActionResult>(result);
        notifier.Verify(n => n.AddAsync(type, It.Is<LocalizedHtmlString>(value => value.Name == message)), Times.Once);
        notifier.VerifyNoOtherCalls();
        Assert.Empty(await harness.LoadRequestsAsync());
    }
}
