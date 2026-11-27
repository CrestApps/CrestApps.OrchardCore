using System.Security.Claims;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Round 2 (Mild): Resume had no status guard, and the lifecycle treats Incomplete as restorable, so a crafted POST made an
/// unpaid subscription bought with a promise to pay active and granted its plan's roles. Only the first payment starts it.
/// </summary>
public sealed class SubscriptionResumeTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Incomplete, false)]
    [InlineData(SubscriptionStatus.Paused, true)]
    public async Task Resume_StartsOnlyASubscriptionThatIsNotWaitingForItsFirstPayment(SubscriptionStatus status, bool resumed)
    {
        // Arrange
        var (controller, lifecycle) = CreateController(status);

        // Act
        await controller.Resume("sub-1");

        // Assert
        lifecycle.Verify(service => service.ResumeAsync("sub-1", It.IsAny<CancellationToken>()), resumed ? Times.Once() : Times.Never());
    }

    /// <summary>
    /// Round 3 (Low): pausing a subscription waiting for its first payment turned it into one Resume starts.
    /// </summary>
    [Theory]
    [InlineData(SubscriptionStatus.Incomplete, false)]
    [InlineData(SubscriptionStatus.Active, true)]
    public async Task Pause_SuspendsOnlyASubscriptionThatIsNotWaitingForItsFirstPayment(SubscriptionStatus status, bool paused)
    {
        // Arrange
        var (controller, lifecycle) = CreateController(status);

        // Act
        await controller.Pause("sub-1", reason: null);

        // Assert
        lifecycle.Verify(service => service.PauseAsync("sub-1", It.IsAny<string>(), It.IsAny<CancellationToken>()), paused ? Times.Once() : Times.Never());
    }

    private static (SubscriptionAgreementsController Controller, Mock<ISubscriptionLifecycleService> Lifecycle) CreateController(SubscriptionStatus status)
    {
        var subscription = new Subscription { ItemId = "sub-1", Status = status };
        var subscriptions = new Mock<ISubscriptionManager>();
        subscriptions.Setup(manager => manager.FindByIdAsync("sub-1", It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        var lifecycle = new Mock<ISubscriptionLifecycleService>();
        var authorization = new Mock<IAuthorizationService>();
        authorization.Setup(service => service.AuthorizeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<object>(), It.IsAny<IEnumerable<IAuthorizationRequirement>>()))
            .ReturnsAsync(AuthorizationResult.Success());

        var htmlLocalizer = new Mock<IHtmlLocalizer<SubscriptionAgreementsController>>();
        htmlLocalizer.Setup(localizer => localizer[It.IsAny<string>()]).Returns((string name) => new LocalizedHtmlString(name, name));

        var stringLocalizer = new Mock<IStringLocalizer<SubscriptionAgreementsController>>();
        stringLocalizer.Setup(localizer => localizer[It.IsAny<string>()]).Returns((string name) => new LocalizedString(name, name));

        var controller = new SubscriptionAgreementsController(
            subscriptions.Object,
            lifecycle.Object,
            authorization.Object,
            Mock.Of<INotifier>(),
            new SubscriptionDisplayText(Array.Empty<ICheckoutPaymentProvider>(), Mock.Of<IStringLocalizer<SubscriptionDisplayText>>()),
            htmlLocalizer.Object,
            stringLocalizer.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };

        return (controller, lifecycle);
    }
}
