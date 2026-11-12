using CrestApps.OrchardCore.Commerce.Emails;
using CrestApps.OrchardCore.Orders.Controllers;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Orders;

/// <summary>
/// SEC-C17h (review): the account made from an order's confirmation took a password from whoever was on the page, and
/// the address came from the order, which anyone could have typed. With email validation off they signed in at once; if
/// the real owner then followed the store's confirmation link, the owner's guest orders joined an account whose password
/// the other person knew. The account now has no password until the owner follows the link and chooses one.
/// </summary>
public sealed class OrderAccountCreationTests
{
    // The confirmation's token is a time-limited data protection token, which checks its expiry by the real clock.
    internal static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public async Task Create_MakesAnAccountWithNoPassword_AndLeavesTheOrderOutOfItUntilTheLinkIsFollowed()
    {
        // Arrange
        var harness = new OrderAccountHarness();

        // Act
        await harness.CreateController().Create(new OrderCreateAccountViewModel { Token = harness.Token }, returnUrl: null);

        // Assert
        harness.Users.Verify(manager => manager.CreateAsync(It.IsAny<IUser>()), Times.Once());
        harness.Users.Verify(manager => manager.CreateAsync(It.IsAny<IUser>(), It.IsAny<string>()), Times.Never());
        Assert.Null(harness.Order.UserId);
        Assert.Equal(1, harness.EmailsSent);
    }

    [Fact]
    public async Task Create_WhenTheStoreCantSendEmail_MakesNoAccount()
    {
        // Arrange: no email would ever reach the owner, so the account could never be finished.
        var harness = new OrderAccountHarness(canSend: false);

        // Act
        await harness.CreateController().Create(new OrderCreateAccountViewModel { Token = harness.Token }, returnUrl: null);

        // Assert
        harness.Users.Verify(manager => manager.CreateAsync(It.IsAny<IUser>()), Times.Never());
        harness.Users.Verify(manager => manager.CreateAsync(It.IsAny<IUser>(), It.IsAny<string>()), Times.Never());
        Assert.Equal(0, harness.EmailsSent);
    }
}

/// <summary>
/// The order account controller with a guest's order, a user manager and an email sender that only count.
/// </summary>
internal sealed class OrderAccountHarness
{
    private readonly ServiceProvider _services;

    public OrderAccountHarness(bool canSend = true)
    {
        Order = new Order { ItemId = "order-1", OrderNumber = "261112-ABC123", CustomerEmail = "pat@example.test", Status = OrderStatus.Paid, Currency = "USD" };

        var clock = Mock.Of<IClock>(value => value.UtcNow == OrderAccountCreationTests.Now);
        var claims = new OrderAccountClaims(SignedLinkDoubles.Tokens(new EphemeralDataProtectionProvider(), clock));
        Token = claims.Create(Order.ItemId);

        var orders = new Mock<IOrderStore>();
        orders.Setup(store => store.FindByIdAsync(Order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(Order);

        Users = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, new List<IUserValidator<IUser>>(), new List<IPasswordValidator<IUser>>(), null, null, null, null);
        Users.Setup(manager => manager.FindByEmailAsync(It.IsAny<string>())).ReturnsAsync((IUser)null);
        Users.Setup(manager => manager.FindByNameAsync(It.IsAny<string>())).ReturnsAsync((IUser)null);
        Users.Setup(manager => manager.CreateAsync(It.IsAny<IUser>())).Callback<IUser>(user => Created = user).ReturnsAsync(IdentityResult.Success);
        Users.Setup(manager => manager.CreateAsync(It.IsAny<IUser>(), It.IsAny<string>())).Callback<IUser, string>((user, _) => Created = user).ReturnsAsync(IdentityResult.Success);
        Users.Setup(manager => manager.GenerateEmailConfirmationTokenAsync(It.IsAny<IUser>())).ReturnsAsync("code-1");
        Users.Setup(manager => manager.UpdateAsync(It.IsAny<IUser>())).ReturnsAsync(IdentityResult.Success);

        var email = new Mock<ICommerceEmailSender>();
        email.SetupGet(sender => sender.CanSend).Returns(canSend);
        email.Setup(sender => sender.SendAsync(It.IsAny<CommerceEmail>(), It.IsAny<Func<CancellationToken, Task<CommerceEmailContent>>>(), It.IsAny<CancellationToken>()))
            .Callback(() => EmailsSent++)
            .ReturnsAsync(true);

        var stringLocalizer = new Mock<IStringLocalizer<OrderAccountController>>();
        stringLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));
        stringLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedString(name, name));

        var htmlLocalizer = new Mock<IHtmlLocalizer<OrderAccountController>>();
        htmlLocalizer.Setup(value => value[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));
        htmlLocalizer.Setup(value => value[It.IsAny<string>(), It.IsAny<object[]>()]).Returns((string name, object[] _) => new LocalizedHtmlString(name, name));

        var limiter = new Mock<IAttemptLimiter>();
        limiter.Setup(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(AttemptLimitResult.Unlimited);

        var notifier = new Mock<INotifier>();
        notifier.Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>())).Returns(ValueTask.CompletedTask);
        notifier.Setup(value => value.AddAsync(It.IsAny<NotifyType>(), It.IsAny<LocalizedHtmlString>(), It.IsAny<NotifyContext>())).Returns(ValueTask.CompletedTask);

        _services = new ServiceCollection()
            .AddSingleton(orders.Object)
            .AddSingleton(claims)
            .AddSingleton(Users.Object)
            .AddSingleton(limiter.Object)
            .AddSingleton(notifier.Object)
            .AddSingleton(email.Object)
            .AddSingleton(clock)
            .AddSingleton(Mock.Of<global::OrchardCore.DisplayManagement.IShapeFactory>())
            .AddSingleton(stringLocalizer.Object)
            .AddSingleton(htmlLocalizer.Object)
            .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>))
            .BuildServiceProvider();
    }

    public Order Order { get; }

    public string Token { get; }

    public Mock<UserManager<IUser>> Users { get; }

    public IUser Created { get; private set; }

    public int EmailsSent { get; private set; }

    public OrderAccountController CreateController()
    {
        var controller = ActivatorUtilities.CreateInstance<OrderAccountController>(_services);
        var url = new Mock<IUrlHelper>();

        url.Setup(helper => helper.IsLocalUrl(It.IsAny<string>())).Returns(true);
        url.Setup(helper => helper.Content(It.IsAny<string>())).Returns("/");
        url.Setup(helper => helper.RouteUrl(It.IsAny<UrlRouteContext>())).Returns("https://shop.example.test/orders/account/activate?userId=u1&code=code-1");
        url.Setup(helper => helper.Action(It.IsAny<UrlActionContext>())).Returns("https://shop.example.test/confirm?code=code-1");

        var httpContext = new DefaultHttpContext { RequestServices = _services };
        httpContext.Request.Scheme = "https";

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = url.Object;

        return controller;
    }
}
