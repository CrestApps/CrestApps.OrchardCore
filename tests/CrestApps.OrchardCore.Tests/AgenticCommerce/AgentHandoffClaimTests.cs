using CrestApps.OrchardCore.AgenticCommerce;
using CrestApps.OrchardCore.AgenticCommerce.Controllers;
using CrestApps.OrchardCore.AgenticCommerce.Core;
using CrestApps.OrchardCore.AgenticCommerce.Core.Models;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.AgenticCommerce.Services;
using CrestApps.OrchardCore.Core.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.AgenticCommerce;

/// <summary>
/// SEC-C17j: an agent's hand-off link gives its checkout to the first browser that opens it. Opened again, by someone
/// else who got the link, it doesn't issue the guest token again and hand them the checkout too.
/// </summary>
public sealed class AgentHandoffClaimTests
{
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();
    // The protector checks expiry against the system clock, so the mock clock must be "now" for the token to open.
    private readonly IClock _clock = Mock.Of<IClock>(clock => clock.UtcNow == DateTime.UtcNow);

    [Fact]
    public async Task Continue_OpenedASecondTime_DoesNotGiveTheCheckoutAgain()
    {
        // Arrange
        var checkout = new AgentCheckout { ItemId = "agent-checkout-1", CheckoutSessionId = "session-1" };
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending, OwnerId = AgentPrincipal.OwnerPrefix + "platform-1" };
        var saves = 0;

        var checkouts = new Mock<IAgentCheckoutStore>();
        checkouts.Setup(store => store.FindByIdAsync(checkout.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(checkout);

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions.Setup(store => store.GetAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(session);
        sessions.Setup(store => store.SaveAsync(It.IsAny<CheckoutSession>(), It.IsAny<CancellationToken>()))
            .Callback(() => saves++)
            .Returns(Task.CompletedTask);

        var token = new SignedTokenService(_protection, _clock).Protect(AgenticCommerceStorage.HandoffTokenPurpose, checkout.ItemId, TimeSpan.FromDays(2));

        // Act: the buyer opens it, then someone else does.
        await CreateController(checkouts.Object, sessions.Object).Continue(token);

        var claimed = session.GuestTokenHash;

        await CreateController(checkouts.Object, sessions.Object).Continue(token);

        // Assert
        Assert.False(string.IsNullOrEmpty(claimed));
        Assert.Equal(claimed, session.GuestTokenHash);
        Assert.Equal(1, saves);
    }

    /// <summary>
    /// SEC-C17j (follow-up): the buyer paid in the browser that claimed the checkout, then opens the link again on their
    /// phone. That browser doesn't own the checkout, so its confirmation would answer 404. It gets a page that says the
    /// order was placed and how to see it, with none of the order's details.
    /// </summary>
    [Fact]
    public async Task Continue_InAnotherBrowserAfterTheOrderWasPlaced_SaysTheOrderIsPlaced()
    {
        // Arrange
        var (checkouts, sessions, token) = Claimed(CheckoutSessionStatus.Completed, ownedByThisBrowser: false);

        // Act
        var result = await CreateController(checkouts, sessions).Continue(token);

        // Assert
        var view = Assert.IsType<ViewResult>(result);

        Assert.Equal("Elsewhere", view.ViewName);
        Assert.Equal(true, view.Model?.GetType().GetProperty("Placed")?.GetValue(view.Model));
    }

    [Fact]
    public async Task Continue_InAnotherBrowserWhileTheCheckoutIsOpen_SaysItIsOpenElsewhere()
    {
        // Arrange
        var (checkouts, sessions, token) = Claimed(CheckoutSessionStatus.Pending, ownedByThisBrowser: false);

        // Act
        var result = await CreateController(checkouts, sessions).Continue(token);

        // Assert
        var view = Assert.IsType<ViewResult>(result);

        Assert.Equal("Elsewhere", view.ViewName);
        Assert.Equal(false, view.Model?.GetType().GetProperty("Placed")?.GetValue(view.Model));
    }

    [Fact]
    public async Task Continue_InTheBrowserThatClaimedIt_AfterTheOrderWasPlaced_ShowsTheConfirmation()
    {
        // Arrange
        var (checkouts, sessions, token) = Claimed(CheckoutSessionStatus.Completed, ownedByThisBrowser: true);

        // Act
        var result = await CreateController(checkouts, sessions).Continue(token);

        // Assert
        Assert.Equal(CheckoutConstants.RouteNames.Confirmation, Assert.IsType<RedirectToRouteResult>(result).RouteName);
    }

    // A checkout the buyer's first browser already claimed: it has a guest token, and only that browser holds it.
    private (IAgentCheckoutStore Checkouts, ICheckoutSessionStore Sessions, string Token) Claimed(CheckoutSessionStatus status, bool ownedByThisBrowser)
    {
        var checkout = new AgentCheckout { ItemId = "agent-checkout-1", CheckoutSessionId = "session-1" };
        var session = new CheckoutSession { SessionId = "session-1", Status = status, GuestTokenHash = "claimed-by-the-first-browser" };

        var checkouts = new Mock<IAgentCheckoutStore>();
        checkouts.Setup(store => store.FindByIdAsync(checkout.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(checkout);

        var sessions = new Mock<ICheckoutSessionStore>();
        sessions.Setup(store => store.GetAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

        // The ownership-checked read finds the checkout only for the browser that holds its token.
        sessions.Setup(store => store.GetAsync("session-1", It.IsAny<CheckoutSessionStatus>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownedByThisBrowser ? session : null);

        var token = new SignedTokenService(_protection, _clock).Protect(AgenticCommerceStorage.HandoffTokenPurpose, checkout.ItemId, TimeSpan.FromDays(2));

        return (checkouts.Object, sessions.Object, token);
    }

    private AgentHandoffController CreateController(IAgentCheckoutStore checkouts, ICheckoutSessionStore sessions)
    {
        var httpContext = new DefaultHttpContext { RequestServices = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceCollection()) };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var controller = new AgentHandoffController(
            new AgentSiteUrls(accessor, Mock.Of<ISiteService>(), new SignedTokenService(_protection, _clock)),
            checkouts,
            sessions,
            new GuestSessionTokenManager(accessor, _protection, _clock),
            Mock.Of<ICheckoutEngine>(),
            Mock.Of<CrestApps.OrchardCore.Throttling.IAttemptLimiter>(limiter => limiter.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()) == Task.FromResult(new CrestApps.OrchardCore.Throttling.AttemptLimitResult(true, 1, TimeSpan.Zero))));

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();

        return controller;
    }
}
