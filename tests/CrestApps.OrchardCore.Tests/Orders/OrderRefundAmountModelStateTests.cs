using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Tests.Core.ModelBinding;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Navigation;
using OrdersStartup = CrestApps.OrchardCore.Orders.Startup;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// G13 (fourth fix verification): the order refund passed a model-bound amount to <see cref="OrderOperationsService.RefundAsync(Order, decimal?, string, bool, string, bool, CancellationToken)"/>
/// without looking at the model state, and the service reads a null amount as "everything still refundable". An amount the
/// binder could not read, such as "1.250,00", left a model error and a null, and refunded the whole order. The post is bound
/// through MVC's own binder under the request culture, into the controller, as a site calls the action.
/// </summary>
public sealed class OrderRefundAmountModelStateTests
{
    [Theory]
    [InlineData("de-DE", "1.250,00")]
    [InlineData("en-US", "1,250.00")]
    [InlineData("fr-FR", "twelve")]
    public async Task AnAmountThatIsNotANumber_IsRefused_AndNothingIsRefunded(string culture, string posted)
    {
        var (lockRequests, notifier, result) = await RefundAsync(culture, posted);

        Assert.Empty(lockRequests);
        Assert.Equal(nameof(OrdersAdminController.Detail), Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Single(notifier.Invocations, invocation => invocation.Arguments.Count > 0 && Equals(invocation.Arguments[0], NotifyType.Error));
    }

    // An empty amount still means "everything still refundable", and a readable one is refunded: both reach the service.
    [Theory]
    [InlineData("de-DE", "")]
    [InlineData("de-DE", "12.50")]
    [InlineData("fr-FR", "12,50")]
    public async Task AnEmptyOrReadableAmount_ReachesTheRefund(string culture, string posted)
    {
        var (lockRequests, _, _) = await RefundAsync(culture, posted);

        Assert.Equal(["ORDER_REFUND_order-1"], lockRequests);
    }

    private static async Task<(List<string> LockRequests, Mock<INotifier> Notifier, IActionResult Result)> RefundAsync(string culture, string posted)
    {
        var order = new Order { ItemId = "order-1", Currency = "USD" };
        var orderStore = new Mock<IOrderStore>();
        orderStore.Setup(store => store.FindByIdAsync("order-1", It.IsAny<CancellationToken>())).ReturnsAsync(order);

        var authorization = new Mock<IAuthorizationService>();
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var notifier = new Mock<INotifier>();
        var htmlLocalizer = new Mock<IHtmlLocalizer<OrdersAdminController>>();
        htmlLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        var controller = new OrdersAdminController(
            orderStore.Object,
            authorization.Object,
            Mock.Of<IShapeFactory>(),
            notifier.Object,
            Options.Create(new PagerOptions()),
            new PassThroughStringLocalizer<OrdersAdminController>(),
            htmlLocalizer.Object,
            Mock.Of<IClock>());

        var method = typeof(OrdersAdminController).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Single(candidate => candidate.Name == nameof(OrdersAdminController.Refund) && candidate.GetCustomAttributes<HttpPostAttribute>().Any());

        var bound = await FormPostBinder.BindAsync(new OrdersStartup(), method, culture, new Dictionary<string, StringValues>
        {
            ["Amount"] = posted,
            ["Reason"] = "Damaged",
        }, controller: controller);

        // The lock is the first thing the refund takes; it is refused here, so a request that reaches it refunds nothing.
        var lockRequests = new List<string>();
        var distributedLock = new Mock<IDistributedLock>();
        distributedLock
            .Setup(value => value.TryAcquireLockAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<TimeSpan?>()))
            .Callback((string key, TimeSpan _, TimeSpan? _) => lockRequests.Add(key))
            .ReturnsAsync((Mock.Of<ILocker>(), false));

        var operations = new OrderOperationsService(
            orderStore.Object,
            orderService: null,
            scope: null,
            confirmationSender: null,
            checkoutEngine: null,
            tenderLedger: null,
            refundService: null,
            attemptStore: null,
            refundStore: null,
            paymentProviders: null,
            distributedLock.Object,
            Mock.Of<IClock>(),
            serviceProvider: null,
            NullLogger<OrderOperationsService>.Instance,
            new PassThroughStringLocalizer<OrderOperationsService>());

        var result = await controller.Refund("order-1", (OrderRefundInputModel)bound.Arguments[1], operations);

        return (lockRequests, notifier, result);
    }
}
