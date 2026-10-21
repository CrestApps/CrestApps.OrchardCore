using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Controllers;
using CrestApps.OrchardCore.Checkout.Models;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Checkout.ViewModels;
using CrestApps.OrchardCore.Tests.Checkout.Fakes;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Taxation.Fakes;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Entities;
using OrchardCore.Locking.Distributed;
using OrchardCore.Settings;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Checkout;

/// <summary>
/// The module that starts a checkout says where the customer goes back to. A session started with a return URL
/// shows a Continue button on the confirmation and a Back link on the payment step and on the page of a checkout
/// that ended unpaid, and a URL that leads off the site is refused before the session exists, so a checkout can
/// never be used to send a customer to another site.
/// </summary>
public sealed class CheckoutReturnUrlTests
{
    private static readonly DateTime _now = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("/")]
    [InlineData("/my/payments")]
    [InlineData("/my/payments?tab=open#due")]
    [InlineData("~/")]
    [InlineData("~/my/payments")]
    public void IsLocalUrl_ForAPathOnThisSite_IsTrue(string url)
    {
        // Act
        var isLocal = CheckoutReturnInfo.IsLocalUrl(url);

        // Assert
        Assert.True(isLocal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://evil.example/my/payments")]
    [InlineData("//evil.example/my/payments")]
    [InlineData("/\\evil.example")]
    [InlineData("~//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("my/payments")]
    [InlineData("/my/\u0001payments")]
    public void IsLocalUrl_ForAnythingElse_IsFalse(string url)
    {
        // Act
        var isLocal = CheckoutReturnInfo.IsLocalUrl(url);

        // Assert
        Assert.False(isLocal);
    }

    [Fact]
    public async Task StartAsync_WithALocalReturnUrl_StoresItOnTheSession()
    {
        // Arrange
        var (engine, _) = CreateEngine();

        // Act
        var session = await engine.StartAsync(
            new StartCheckoutRequest
            {
                ReferenceType = "Fee",
                ReferenceId = "fee-1",
                ReturnUrl = "/my/payments",
                ReturnText = "Back to my payments",
                Title = "Payment received",
            },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(session.TryGet<CheckoutReturnInfo>(out var returnInfo));
        Assert.Equal("/my/payments", returnInfo.ReturnUrl);
        Assert.Equal("Back to my payments", returnInfo.ReturnText);
        Assert.Equal("Payment received", returnInfo.Title);
    }

    [Theory]
    [InlineData("https://evil.example/my/payments")]
    [InlineData("//evil.example/my/payments")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    public async Task StartAsync_WithAReturnUrlThatIsNotLocal_IsRefusedBeforeASessionExists(string returnUrl)
    {
        // Arrange
        var (engine, sessionStore) = CreateEngine();

        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => engine.StartAsync(
            new StartCheckoutRequest
            {
                ReferenceType = "Fee",
                ReferenceId = "fee-1",
                ReturnUrl = returnUrl,
            },
            TestContext.Current.CancellationToken));

        // Assert
        Assert.Equal("request", exception.ParamName);
        Assert.Equal(0, sessionStore.SaveCount);
        Assert.Null(await sessionStore.GetByReferenceAsync("Fee", "fee-1", cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StartAsync_WithoutReturnDetails_StoresNothing()
    {
        // Arrange
        var (engine, _) = CreateEngine();

        // Act
        var session = await engine.StartAsync(new StartCheckoutRequest { ReferenceType = "Fee", ReferenceId = "fee-1" }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(session.TryGet<CheckoutReturnInfo>(out _));
        Assert.Null(CheckoutReturnInfo.GetFrom(session));
    }

    [Fact]
    public void GetBackLink_OnThePaymentStep_ShowsTheReturnUrl()
    {
        // Arrange
        var session = CreateSession(CheckoutSessionStatus.Pending, "/my/payments");

        // Act
        var backLink = CheckoutReturnLinks.GetBackLink(new CheckoutFlow(session));

        // Assert
        Assert.NotNull(backLink);
        Assert.Equal("/my/payments", backLink.ReturnUrl);
        Assert.Equal("Back to my payments", backLink.ReturnText);
    }

    [Fact]
    public void GetBackLink_OnThePaymentStepAfterEarlierSteps_StillShowsTheReturnUrl()
    {
        // Arrange
        var session = CreateSession(CheckoutSessionStatus.Pending, "/my/payments");
        session.Steps.Insert(0, new CheckoutFlowStep { Key = "Contact", Order = 0, CollectData = true });
        session.SavedSteps["Contact"] = new System.Text.Json.Nodes.JsonObject();
        session.CurrentStep = CheckoutConstants.PaymentStepKey;

        var flow = new CheckoutFlow(session);

        // Act
        var backLink = CheckoutReturnLinks.GetBackLink(flow);

        // Assert
        Assert.NotNull(flow.GetPreviousStep());
        Assert.NotNull(backLink);
    }

    [Fact]
    public void GetBackLink_OnAMiddleStepThatHasItsOwnBack_ShowsNothing()
    {
        // Arrange
        var session = CreateSession(CheckoutSessionStatus.Pending, "/my/payments");
        session.Steps.Insert(0, new CheckoutFlowStep { Key = "Contact", Order = 0, CollectData = true });
        session.Steps.Insert(1, new CheckoutFlowStep { Key = "Details", Order = 1, CollectData = true });
        session.SavedSteps["Contact"] = new System.Text.Json.Nodes.JsonObject();
        session.CurrentStep = "Details";

        // Act
        var backLink = CheckoutReturnLinks.GetBackLink(new CheckoutFlow(session));

        // Assert
        Assert.Null(backLink);
    }

    [Fact]
    public void GetBackLink_WithoutAReturnUrl_ShowsNothing()
    {
        // Arrange
        var session = CreateSession(CheckoutSessionStatus.Pending, returnUrl: null);

        // Act
        var backLink = CheckoutReturnLinks.GetBackLink(new CheckoutFlow(session));

        // Assert
        Assert.Null(backLink);
    }

    [Fact]
    public void GetFrom_IgnoresAStoredUrlThatIsNotLocal()
    {
        // Arrange: a value that bypassed the engine, for example one written straight to the store.
        var session = CreateSession(CheckoutSessionStatus.Pending, "https://evil.example/");

        // Act
        var returnInfo = CheckoutReturnInfo.GetFrom(session);
        var backLink = CheckoutReturnLinks.GetBackLink(new CheckoutFlow(session));

        // Assert
        Assert.Null(returnInfo.ReturnUrl);
        Assert.Null(backLink);
    }

    [Fact]
    public async Task Confirmation_OfASessionStartedWithAReturnUrl_CarriesTheContinueLinkAndTitle()
    {
        // Arrange
        var sessionStore = new InMemoryCheckoutSessionStore();
        sessionStore.Seed(CreateSession(CheckoutSessionStatus.Completed, "/my/payments"));
        var controller = CreateController(sessionStore);

        // Act
        var result = await controller.Confirmation("session-1", new InMemoryPaymentAttemptStore(), Mock.Of<ICheckoutPaymentProviderResolver>());

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<CheckoutViewModel>(view.Model);

        Assert.Equal("/my/payments", model.ReturnUrl);
        Assert.Equal("Back to my payments", model.ReturnText);
        Assert.Equal("Payment received", model.Title);
    }

    [Fact]
    public async Task Confirmation_OfASessionStartedWithoutOne_CarriesNoLink()
    {
        // Arrange
        var sessionStore = new InMemoryCheckoutSessionStore();
        sessionStore.Seed(CreateSession(CheckoutSessionStatus.Completed, returnUrl: null));
        var controller = CreateController(sessionStore);

        // Act
        var result = await controller.Confirmation("session-1", new InMemoryPaymentAttemptStore(), Mock.Of<ICheckoutPaymentProviderResolver>());

        // Assert
        var model = Assert.IsType<CheckoutViewModel>(Assert.IsType<ViewResult>(result).Model);

        Assert.Null(model.ReturnUrl);
        Assert.Null(model.Title);
    }

    [Theory]
    [InlineData(CheckoutSessionStatus.Canceled)]
    [InlineData(CheckoutSessionStatus.Expired)]
    [InlineData(CheckoutSessionStatus.Failed)]
    public async Task Display_OfACheckoutThatEndedUnpaid_ShowsThePageWithTheBackLink(CheckoutSessionStatus status)
    {
        // Arrange
        var sessionStore = new InMemoryCheckoutSessionStore();
        sessionStore.Seed(CreateSession(status, "/my/payments"));
        var controller = CreateController(sessionStore);

        // Act
        var result = await controller.Display("session-1", step: null);

        // Assert
        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsAssignableFrom<CheckoutEndedViewModel>(view.Model);

        Assert.Equal("Ended", view.ViewName);
        Assert.Equal(status, model.Status);
        Assert.Equal("/my/payments", model.ReturnUrl);
        Assert.Equal("Back to my payments", model.ReturnText);
    }

    [Fact]
    public async Task Display_OfACompletedCheckout_RedirectsToTheConfirmation()
    {
        // Arrange
        var sessionStore = new InMemoryCheckoutSessionStore();
        sessionStore.Seed(CreateSession(CheckoutSessionStatus.Completed, "/my/payments"));
        var controller = CreateController(sessionStore);

        // Act
        var result = await controller.Display("session-1", step: null);

        // Assert
        var redirect = Assert.IsType<RedirectToRouteResult>(result);

        Assert.Equal(CheckoutConstants.RouteNames.Confirmation, redirect.RouteName);
    }

    [Fact]
    public async Task Display_OfAnUnknownCheckout_IsNotFound()
    {
        // Arrange
        var controller = CreateController(new InMemoryCheckoutSessionStore());

        // Act
        var result = await controller.Display("missing", step: null);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Theory]
    [InlineData("Checkout/Confirmation.cshtml", "Model.ReturnUrl")]
    [InlineData("Checkout/Confirmation.cshtml", "Model.Title")]
    [InlineData("CheckoutEnded.cshtml", "Model.ReturnUrl")]
    [InlineData("CheckoutFlowButtons.cshtml", "CheckoutReturnLinks.GetBackLink(Model.Value)")]
    public void Views_RenderTheReturnLink(string view, string expected)
    {
        // Arrange
        var path = Path.Combine(GetViewsRoot(), view.Replace('/', Path.DirectorySeparatorChar));

        // Act
        var content = File.ReadAllText(path);

        // Assert
        Assert.Contains(expected, content, StringComparison.Ordinal);
    }

    private static CheckoutSession CreateSession(CheckoutSessionStatus status, string returnUrl)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            ReferenceType = "Fee",
            ReferenceId = "fee-1",
            Status = status,
            Currency = "USD",
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = CheckoutConstants.PaymentStepKey,
            Order = int.MaxValue,
            CollectData = false,
        });

        if (returnUrl is not null)
        {
            session.Put(new CheckoutReturnInfo
            {
                ReturnUrl = returnUrl,
                ReturnText = "Back to my payments",
                Title = "Payment received",
            });
        }

        return session;
    }

    private static (DefaultCheckoutEngine Engine, InMemoryCheckoutSessionStore SessionStore) CreateEngine()
    {
        var sessionStore = new InMemoryCheckoutSessionStore();
        var attemptStore = new InMemoryPaymentAttemptStore();
        var providerResolver = Mock.Of<ICheckoutPaymentProviderResolver>();

        var engine = new DefaultCheckoutEngine(
            sessionStore,
            attemptStore,
            providerResolver,
            Mock.Of<ICheckoutRecurringPaymentProviderResolver>(),
            new CheckoutReconciliationService(attemptStore, providerResolver, NullLogger<CheckoutReconciliationService>.Instance),
            Mock.Of<ICheckoutRefundService>(),
            [],
            Mock.Of<IDistributedLock>(),
            Mock.Of<ISession>(),
            new TestClock(_now),
            NullLogger<DefaultCheckoutEngine>.Instance);

        return (engine, sessionStore);
    }

    private static CheckoutController CreateController(InMemoryCheckoutSessionStore sessionStore)
    {
        var updateModelAccessor = new Mock<IUpdateModelAccessor>();
        updateModelAccessor.SetupGet(accessor => accessor.ModelUpdater).Returns(Mock.Of<IUpdateModel>());

        return new CheckoutController(
            sessionStore,
            Mock.Of<ICheckoutEngine>(),
            Mock.Of<IDisplayManager<CheckoutFlow>>(),
            updateModelAccessor.Object,
            new ModelShapeFactory(),
            Mock.Of<INotifier>(),
            StepsLayoutSiteService(),
            new TestClock(_now),
            NullLogger<CheckoutController>.Instance,
            Mock.Of<IHtmlLocalizer<CheckoutController>>());
    }

    // The stepped layout, the default: a page per step.
    private static ISiteService StepsLayoutSiteService()
    {
        var site = new Mock<ISite>();
        site.Setup(s => s.GetOrCreate<CheckoutSettings>()).Returns(new CheckoutSettings());

        var siteService = new Mock<ISiteService>();
        siteService.Setup(s => s.GetSiteSettingsAsync()).ReturnsAsync(site.Object);

        return siteService.Object;
    }

    private static string GetViewsRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "src", "Modules", "CrestApps.OrchardCore.Checkout", "Views");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Checkout module's views from the test output directory.");
    }
}
