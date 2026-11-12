using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.PointOfSale.Api;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Tests.PointOfSale;

/// <summary>
/// SEC-C15: refunding a sale at the till, and paying cash back out of the drawer, takes its own permission, and a
/// cashier returns only sales of their own store unless they manage orders.
/// </summary>
public sealed class PosReturnAuthorizationTests
{
    private readonly Mock<IOrderOperationsService> _operations = new();
    private readonly HashSet<string> _granted = new(StringComparer.Ordinal) { "SellAtRegister" };

    private readonly Register _register = new() { ItemId = "register-a", Name = "Till 1", LocationId = "store-a", IsEnabled = true };

    public PosReturnAuthorizationTests()
    {
        var result = new OrderOperationResult();

        result.Errors.Add("Not refunded in this test.");
        _operations
            .Setup(service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);
    }

    [Fact]
    public async Task Return_SellerWithoutTheRefundPermission_IsForbidden()
    {
        // Arrange
        var order = CreateOrder(locationId: "store-a");

        // Act
        var result = await ReturnAsync(order);

        // Assert
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        AssertNotRefunded();
    }

    [Fact]
    public async Task Return_SaleOfAnotherStore_IsRefused()
    {
        // Arrange
        _granted.Add("RefundAtRegister");
        var order = CreateOrder(locationId: "store-b");

        // Act
        await ReturnAsync(order);

        // Assert
        AssertNotRefunded();
    }

    [Fact]
    public async Task Return_SaleOfAnotherStore_ByStaffWhoManageOrders_IsRefunded()
    {
        // Arrange
        _granted.Add("RefundAtRegister");
        _granted.Add("ManageOrders");
        var order = CreateOrder(locationId: "store-b");

        // Act
        await ReturnAsync(order);

        // Assert
        AssertRefunded();
    }

    [Fact]
    public async Task Return_SaleOfTheSameStore_WithThePermission_IsRefunded()
    {
        // Arrange
        _granted.Add("RefundAtRegister");
        var order = CreateOrder(locationId: "store-a");

        // Act
        await ReturnAsync(order);

        // Assert
        AssertRefunded();
    }

    private void AssertNotRefunded()
        => _operations.Verify(
            service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);

    private void AssertRefunded()
        => _operations.Verify(
            service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Once);

    private static Order CreateOrder(string locationId)
        => new()
        {
            ItemId = "order-1",
            OrderNumber = "1001",
            Status = OrderStatus.Paid,
            Channel = OrderChannels.PointOfSale,
            RegisterId = locationId == "store-a" ? "register-a" : "register-b",
            LocationId = locationId,
            Currency = "USD",
        };

    private async Task<object> ReturnAsync(Order order)
    {
        var orders = new Mock<IOrderStore>();
        var registers = new Mock<IRegisterStore>();
        var shiftStore = new Mock<IRegisterShiftStore>();
        var authorization = new Mock<IAuthorizationService>();

        orders.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        registers.Setup(store => store.FindByIdAsync(_register.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_register);
        registers.Setup(store => store.FindByIdAsync("register-b", It.IsAny<CancellationToken>())).ReturnsAsync(new Register { ItemId = "register-b", LocationId = "store-b", IsEnabled = true });
        shiftStore.Setup(store => store.FindOpenAsync(_register.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(new RegisterShift { ItemId = "shift-1", RegisterId = _register.ItemId });
        authorization
            .Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync((ClaimsPrincipal _, object _, IEnumerable<IAuthorizationRequirement> requirements) =>
                requirements.OfType<PermissionRequirement>().All(requirement => _granted.Contains(requirement.Permission.Name))
                    ? AuthorizationResult.Success()
                    : AuthorizationResult.Failed());

        var services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .AddSingleton(registers.Object)
            .AddSingleton(shiftStore.Object)
            .AddSingleton(authorization.Object)
            .AddSingleton(_operations.Object)
            .AddSingleton(Mock.Of<global::CrestApps.OrchardCore.Checkout.Services.ICheckoutTenderLedger>())
            .AddSingleton(Mock.Of<global::OrchardCore.Locking.Distributed.IDistributedLock>())
            .AddSingleton(Mock.Of<global::YesSql.ISession>())
            .AddSingleton(Mock.Of<global::OrchardCore.Settings.ISiteService>())
            .AddSingleton(Mock.Of<global::OrchardCore.Modules.IClock>())
            .AddSingleton<PosShiftService>()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services,
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "cashier")], "test")),
        };
        var request = new ApiPosReturnRequest { OrderId = order.ItemId, RegisterId = _register.ItemId };
        var method = typeof(PosApiEndpoints).GetMethod("ReturnAsync", BindingFlags.NonPublic | BindingFlags.Static);
        var arguments = method.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(ApiPosReturnRequest) ? request
                : parameter.ParameterType == typeof(HttpContext) ? httpContext
                : services.GetRequiredService(parameter.ParameterType))
            .ToArray();

        var task = (Task)method.Invoke(null, arguments);

        await task;

        var result = task.GetType().GetProperty("Result").GetValue(task);

        // A Results<...> union carries the result it holds.
        return result.GetType().GetProperty("Result")?.GetValue(result) ?? result;
    }
}
