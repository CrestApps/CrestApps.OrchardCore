using System.Reflection;
using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.PointOfSale;
using CrestApps.OrchardCore.PointOfSale.Api;
using CrestApps.OrchardCore.PointOfSale.Models;
using CrestApps.OrchardCore.PointOfSale.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
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
    private readonly RegisterShift _shift = new() { ItemId = "shift-1", RegisterId = "register-a", Status = RegisterShiftStatus.Open };
    private readonly Mock<ICheckoutTenderLedger> _ledger = new();

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

    /// <summary>
    /// An order with no location whose register can't be found said nothing about its store, and matched every register
    /// with no location.
    /// </summary>
    [Fact]
    public async Task Return_SaleWhoseStoreIsUnknown_AtARegisterWithNoLocation_IsRefused()
    {
        // Arrange
        _granted.Add("RefundAtRegister");
        _register.LocationId = null;
        var order = CreateOrder(locationId: null);
        order.RegisterId = "register-removed";

        // Act
        await ReturnAsync(order);

        // Assert
        AssertNotRefunded();
    }

    /// <summary>
    /// M3 (final review): the cash given back was what the sale's cash hold held before the refund, read before any lock,
    /// less what it held after. When another return of the same sale gave the cash back in between, this one returned
    /// nothing, yet both answers said the full cash back and the drawer paid it out twice.
    /// </summary>
    [Fact]
    public async Task Return_WhenAnotherReturnGaveTheCashBackFirst_TakesNoCashFromTheDrawer()
    {
        // Arrange: before the refund the sale still held 50 in cash; the other return put it back, and this one returned
        // nothing.
        _granted.Add("RefundAtRegister");
        var order = CreateOrder(locationId: "store-a");
        order.CheckoutSessionId = "session-1";

        _ledger.SetupSequence(ledger => ledger.GetRedeemedAsync("session-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new CheckoutTenderHold { SessionId = "session-1", ProviderKey = PosConstants.CashTenderKey, Reference = "cash", Amount = 50m, Currency = "USD", Status = CheckoutTenderHoldStatus.Redeemed }])
            .ReturnsAsync([]);
        _operations
            .Setup(service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderOperationResult());

        // Act
        var result = await ReturnAsync(order);

        // Assert
        var ok = Assert.IsType<Ok<ApiPosReturn>>(result);
        Assert.Equal(0m, ok.Value.CashBack.Amount);
        Assert.Empty(_shift.CashMovements);
    }

    [Fact]
    public async Task Return_PaysOutTheCashTheRefundPutBackOnTheSale()
    {
        // Arrange
        _granted.Add("RefundAtRegister");
        var order = CreateOrder(locationId: "store-a");
        order.CheckoutSessionId = "session-1";

        var refunded = new OrderOperationResult { Refunded = 20m };
        refunded.ReturnedToBalances[PosConstants.CashTenderKey] = 20m;
        _operations
            .Setup(service => service.RefundAsync(It.IsAny<Order>(), It.IsAny<decimal?>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refunded);

        // Act
        var result = await ReturnAsync(order);

        // Assert
        var ok = Assert.IsType<Ok<ApiPosReturn>>(result);
        Assert.Equal(20m, ok.Value.CashBack.Amount);
        Assert.Equal(-20m, Assert.Single(_shift.CashMovements).Amount);
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
        shiftStore.Setup(store => store.FindOpenAsync(_register.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(_shift);
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
            .AddSingleton(_ledger.Object)
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
