using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Controllers;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// Review 11 (M-one-page-step-payment): a one-page checkout post saves every section of the page, but a post naming the
/// payment step skipped the release of a payment begun for the old total, as if it were only the payment. Every one-page
/// post now releases that payment before it changes anything, and is refused while the payment is in progress.
/// </summary>
public sealed class OnePageCheckoutPaymentReleaseTests
{
    private static readonly DateTime _now = new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AOnePagePostNamingThePaymentStep_WhileAPaymentIsInProgress_ChangesNothing()
    {
        // Arrange
        var sessionStore = new InMemoryCheckoutSessionStore();
        var session = new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending, Currency = "USD" };

        session.Steps.Add(new CheckoutFlowStep { Key = "details", Order = 0, CollectData = true });
        session.Steps.Add(new CheckoutFlowStep { Key = CheckoutConstants.PaymentStepKey, Order = int.MaxValue, CollectData = false });
        sessionStore.Seed(session);

        var releaser = new Mock<ICheckoutPaymentReleaseService>();
        releaser.Setup(service => service.ReleaseAsync("session-1", It.IsAny<CancellationToken>())).ReturnsAsync(CheckoutPaymentReleaseResult.PaymentInProgress);

        var displayManager = new Mock<IDisplayManager<CheckoutFlow>>();
        var updater = new Mock<IUpdateModel>();
        updater.SetupGet(model => model.ModelState).Returns(new ModelStateDictionary());

        var updateModelAccessor = new Mock<IUpdateModelAccessor>();
        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(updater.Object);

        var controller = new CheckoutController(
            sessionStore,
            Mock.Of<ICheckoutEngine>(),
            releaser.Object,
            displayManager.Object,
            updateModelAccessor.Object,
            new ModelShapeFactory(),
            Mock.Of<INotifier>(),
            OnePageSiteService(),
            new TestClock(_now),
            NullLogger<CheckoutController>.Instance,
            Mock.Of<IHtmlLocalizer<CheckoutController>>());

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Form = new FormCollection([]);
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.DisplayPost(new CheckoutViewModel { SessionId = "session-1", Step = CheckoutConstants.PaymentStepKey });

        // Assert
        Assert.IsType<RedirectToRouteResult>(result);
        releaser.Verify(service => service.ReleaseAsync("session-1", It.IsAny<CancellationToken>()), Times.Once());
        displayManager.Verify(manager => manager.UpdateEditorAsync(It.IsAny<CheckoutFlow>(), It.IsAny<IUpdateModel>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never());
    }

    private static ISiteService OnePageSiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings { Layout = CheckoutLayout.OnePage });

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }
}
