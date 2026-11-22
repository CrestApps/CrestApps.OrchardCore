using System.Security.Claims;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Locking;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// Review 11 (M-cart-checkout-limit): checking out a cart places an order that holds its stock, but it skipped the limit
/// on checkouts started per network that every other checkout start takes. A bot with guest carts could hold all the
/// stock when the Rate Limits feature was off.
/// </summary>
public sealed class CartCheckoutStartLimitTests
{
    [Fact]
    public async Task CheckingOutCartsFromOneNetwork_IsRefusedOnceItsBudgetIsSpent_BeforeAnyStockIsHeld()
    {
        // Arrange
        var options = new AttemptLimitOptions();
        options.AddPolicy(CheckoutConstants.AttemptPolicies.CheckoutStartNetwork, 60, TimeSpan.FromHours(1));

        var limiter = new DistributedAttemptLimiter(
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            new LocalLock(NullLogger<LocalLock>.Instance),
            new ShellSettings { Name = "Default" },
            Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow),
            Options.Create(options),
            NullLogger<DistributedAttemptLimiter>.Instance);

        var cart = new Cart();
        var carts = new Mock<ICartService>();
        carts.Setup(service => service.GetCurrentAsync(It.IsAny<CancellationToken>())).ReturnsAsync(cart);
        carts.Setup(service => service.PriceAsync(cart, It.IsAny<CancellationToken>())).ReturnsAsync(new CartPricing
        {
            Cart = cart,
            Currency = "USD",
            Lines = [new CartLinePricing { Line = new CartLine { Quantity = 1 }, UnitPrice = 10m, LineTotal = 10m }],
        });

        // Each order that is placed holds stock; here the last unit is always gone, so nothing else runs.
        var orders = new Mock<IOrderService>();
        orders.Setup(service => service.CreateFromCartAsync(It.IsAny<CartPricing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderCreationResult(null, ["Out of stock."]));

        var service = new CartCheckoutService(
            carts.Object,
            orders.Object,
            Mock.Of<ICheckoutEngine>(),
            new UserManager<IUser>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null),
            Mock.Of<global::YesSql.ISession>(),
            Mock.Of<IServiceProvider>(),
            limiter,
            new PassThroughStringLocalizer<CartCheckoutService>());

        var network = ClientNetworkKey.FromAddress("203.0.113.7");
        var results = new List<CartCheckoutResult>();

        // Act
        for (var i = 0; i < 61; i++)
        {
            results.Add(await service.StartAsync(new ClaimsPrincipal(), network, TestContext.Current.CancellationToken));
        }

        var otherNetwork = await service.StartAsync(new ClaimsPrincipal(), ClientNetworkKey.FromAddress("198.51.100.9"), TestContext.Current.CancellationToken);

        // Assert
        Assert.All(results.Take(60), result => Assert.Equal(CartCheckoutFailure.OutOfStock, result.Failure));
        Assert.Equal(CartCheckoutFailure.TooManyRequests, results[^1].Failure);
        Assert.True(results[^1].RetryAfter > TimeSpan.Zero);
        Assert.Equal(CartCheckoutFailure.OutOfStock, otherNetwork.Failure);
        orders.Verify(service => service.CreateFromCartAsync(It.IsAny<CartPricing>(), It.IsAny<CancellationToken>()), Times.Exactly(61));
    }
}
