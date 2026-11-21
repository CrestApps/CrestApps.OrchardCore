using System.Reflection;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Payments.Models;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Controllers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Throttling;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// SEC-C17g: following a plan's sign-up link (a link scanner, a mail client's preview, a crawler) starts no checkout.
/// The link shows the plan with a button, and only the button's post, with its antiforgery token, starts one, limited
/// per network the way an order's pay link is.
/// </summary>
public sealed class SubscriptionSignupLinkTests
{
    [Fact]
    public async Task Signup_FollowedByALink_StartsNoCheckout_AndShowsTheButton()
    {
        // Arrange
        var starts = 0;
        var controller = CreateController(() => starts++);

        // Act
        var result = await controller.Signup("plan-1", priceId: null);

        // Assert
        Assert.Equal(0, starts);
        Assert.IsType<ViewResult>(result);
    }

    [Fact]
    public async Task SignupPost_StartsTheCheckout()
    {
        // Arrange
        var starts = 0;
        var controller = CreateController(() => starts++);

        // Act
        var result = await controller.SignupPost("plan-1", priceId: null);

        // Assert
        Assert.Equal(1, starts);
        Assert.Equal("session-1", Assert.IsType<RedirectToRouteResult>(result).RouteValues["sessionId"]);
    }

    [Fact]
    public void SignupPost_NeedsTheAntiforgeryToken()
    {
        // Arrange
        var method = typeof(SubscriptionsController).GetMethod(nameof(SubscriptionsController.SignupPost));

        // Assert
        Assert.NotNull(method.GetCustomAttribute<HttpPostAttribute>());
        Assert.NotNull(method.GetCustomAttribute<ValidateAntiForgeryTokenAttribute>());
    }

    [Fact]
    public async Task SignupPost_OverTheNetworkLimit_StartsNoCheckout()
    {
        // Arrange
        var starts = 0;
        var controller = CreateController(() => starts++, new AttemptLimitResult(false, 0, TimeSpan.FromMinutes(1)));

        // Act
        await controller.SignupPost("plan-1", priceId: null);

        // Assert
        Assert.Equal(0, starts);
        Assert.Equal(StatusCodes.Status429TooManyRequests, controller.HttpContext.Response.StatusCode);
    }

    private static SubscriptionsController CreateController(Action started, AttemptLimitResult? limit = null)
        => CreateController(_ => started(), limit);

    internal static SubscriptionsController CreateController(Action<StartCheckoutRequest> started, AttemptLimitResult? limit = null)
    {
        var plan = new ContentItem { ContentType = "Plan", ContentItemId = "plan-1", ContentItemVersionId = "plan-1-v1", DisplayText = "Starter plan" };
        plan.Apply(nameof(SubscriptionPart), new SubscriptionPart { BillingDuration = 1, DurationType = DurationType.Month });

        var contentManager = new Mock<IContentManager>();
        contentManager.Setup(manager => manager.GetAsync("plan-1", It.IsAny<VersionOptions>())).ReturnsAsync(plan);

        var priceResolver = new Mock<IPriceResolver>();
        priceResolver.Setup(resolver => resolver.ResolveAsync(It.IsAny<ProductSnapshotContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PriceResult(9.99m, "USD", 1, null));

        var engine = new Mock<ICheckoutEngine>();
        engine.Setup(value => value.StartAsync(It.IsAny<StartCheckoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((StartCheckoutRequest request, CancellationToken _) =>
            {
                started(request);

                return new CheckoutSession { SessionId = "session-1", Status = CheckoutSessionStatus.Pending };
            });

        var limiter = new Mock<IAttemptLimiter>();
        limiter.Setup(value => value.AcquireAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(limit ?? AttemptLimitResult.Unlimited);

        var services = new ServiceCollection()
            .AddSingleton(contentManager.Object)
            .AddSingleton(Mock.Of<IContentDefinitionManager>())
            .AddSingleton(engine.Object)
            .AddSingleton(priceResolver.Object)
            .AddSingleton(Mock.Of<IShapeFactory>())
            .AddSingleton(limiter.Object)
            .AddSingleton<ILogger<SubscriptionsController>>(NullLogger<SubscriptionsController>.Instance)
            .BuildServiceProvider();

        var controller = ActivatorUtilities.CreateInstance<SubscriptionsController>(services);

        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } };
        controller.Url = Mock.Of<IUrlHelper>();
        controller.TempData = Mock.Of<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataDictionary>();

        return controller;
    }
}
