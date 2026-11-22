using System.Net;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Core.Throttling;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Endpoints;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Review 11 (M-agent-installment-network): an installment plan's down payment began with no client network, so the
/// card-testing limits on a network (declines counted per network, refused once they reach the limit) never applied to a
/// card typed in on that page.
/// </summary>
public sealed class InstallmentDownPaymentNetworkKeyTests
{
    [Fact]
    public async Task BeginningADownPayment_PassesTheRequestsNetwork_ToTheCheckout()
    {
        // Arrange
        var plan = new InstallmentPlan
        {
            ItemId = "plan-1",
            Status = InstallmentPlanStatus.Draft,
            DownPaymentSessionId = "session-1",
        };

        var plans = new Mock<IInstallmentPlanStore>();
        plans.Setup(store => store.FindByIdAsync("plan-1", It.IsAny<CancellationToken>())).ReturnsAsync(plan);

        var provider = new Mock<ICheckoutPaymentProvider>();
        provider.SetupGet(value => value.Key).Returns("card");
        provider.SetupGet(value => value.Capabilities).Returns(new PaymentProviderCapabilities { SupportsSavedPaymentMethods = true, SupportsEmbeddedElements = true });

        var resolver = new Mock<ICheckoutPaymentProviderResolver>();
        resolver.Setup(value => value.GetProviders()).Returns([provider.Object]);

        var saved = new Mock<ICheckoutSavedPaymentMethodProvider>();
        saved.SetupGet(value => value.Key).Returns("card");

        BeginPaymentOptions begun = null;
        var engine = new Mock<ICheckoutEngine>();
        engine.Setup(value => value.BeginPaymentAsync("session-1", It.IsAny<BeginPaymentOptions>(), It.IsAny<CancellationToken>()))
            .Callback((string _, BeginPaymentOptions options, CancellationToken _) => begun = options)
            .ReturnsAsync(PaymentBeginOutcome.Failure("Declined."));

        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(value => value.AuthorizeAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var httpContext = new DefaultHttpContext { RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider() };
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.7");

        // Act
        await InstallmentPlanPaymentEndpoints.BeginPaymentAsync(
            "plan-1",
            new BeginDownPaymentRequest { ProviderKey = "card" },
            httpContext,
            authorization.Object,
            Mock.Of<IAntiforgery>(),
            plans.Object,
            engine.Object,
            resolver.Object,
            [saved.Object],
            new PassThroughStringLocalizer<InstallmentPlansController>());

        // Assert
        Assert.NotNull(begun);
        Assert.Equal(ClientNetworkKey.FromAddress("203.0.113.7"), begun.ClientNetworkKey);
    }
}
