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
    private readonly IClock _clock = Mock.Of<IClock>(clock => clock.UtcNow == new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc));

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

    private AgentHandoffController CreateController(IAgentCheckoutStore checkouts, ICheckoutSessionStore sessions)
    {
        var httpContext = new DefaultHttpContext { RequestServices = Microsoft.Extensions.DependencyInjection.ServiceCollectionContainerBuilderExtensions.BuildServiceProvider(new Microsoft.Extensions.DependencyInjection.ServiceCollection()) };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var controller = new AgentHandoffController(
            new AgentSiteUrls(accessor, Mock.Of<ISiteService>(), new SignedTokenService(_protection, _clock)),
            checkouts,
            sessions,
            new GuestSessionTokenManager(accessor, _protection, _clock),
            Mock.Of<ICheckoutEngine>());

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.Url = Mock.Of<IUrlHelper>();

        return controller;
    }
}
