using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Tests.Checkout;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Json;
using OrchardCore.Settings;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// Whether the account step applies is a fact about the checkout, not about the request that loaded it.
/// </summary>
/// <remarks>
/// A provider webhook and the reconciliation sweep both complete checkouts with no signed-in user at all.
/// Deciding from the request made them see a registration step the signed-in buyer never had, refuse to
/// complete a purchase that was already paid for, and leave it pending forever.
/// </remarks>
public sealed class UserRegistrationCheckoutHandlerTests
{
    /// <summary>
    /// A checkout that has an owner never needs the account step, whoever loads it.
    /// </summary>
    [Fact]
    public async Task InitializingAsync_WithNoRequest_ConcealsTheStepForAnOwnedCheckout()
    {
        var handler = CreateHandler(httpContext: null);
        var session = CreateSession(ownerId: "owner-1");

        await handler.InitializingAsync(new CheckoutFlowInitializingContext(new CheckoutFlow(session)));

        Assert.True(Assert.Single(session.Steps).Conceal);
    }

    /// <summary>
    /// A checkout with no owner needs the step, even when an unrelated user happens to be signed in.
    /// </summary>
    [Fact]
    public async Task InitializingAsync_ShowsTheStepForAnOwnerlessCheckout()
    {
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity("test"));

        var handler = CreateHandler(context);
        var session = CreateSession(ownerId: null);

        await handler.InitializingAsync(new CheckoutFlowInitializingContext(new CheckoutFlow(session)));

        Assert.False(Assert.Single(session.Steps).Conceal);
    }

    /// <summary>
    /// The engine skips concealed steps when deciding what still blocks completion, so this is the same
    /// rule the webhook path depends on: an owned checkout is never blocked by the account step.
    /// </summary>
    [Fact]
    public async Task ActivatingAsync_ConcealsTheStepWhenTheBuyerIsAlreadyKnown()
    {
        var handler = CreateHandler(httpContext: null);
        var session = new CheckoutSession { SessionId = "session-1", ReferenceType = SubscriptionCheckout.ReferenceType, OwnerId = "owner-1" };

        await handler.ActivatingAsync(new CheckoutFlowActivatingContext(session));

        Assert.True(Assert.Single(session.Steps).Conceal);
    }

    private static CheckoutSession CreateSession(string ownerId)
    {
        var session = new CheckoutSession
        {
            SessionId = "session-1",
            ReferenceType = SubscriptionCheckout.ReferenceType,
            OwnerId = ownerId,
            Status = CheckoutSessionStatus.Pending,
        };

        session.Steps.Add(new CheckoutFlowStep
        {
            Key = SubscriptionConstants.StepKey.UserRegistration,
            Order = 2,
            CollectData = true,
        });

        return session;
    }

    private static UserRegistrationCheckoutHandler CreateHandler(HttpContext httpContext)
    {
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);

        var signInManager = new Mock<SignInManager<IUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
            null,
            null,
            null,
            null);

        var accessor = new Mock<IHttpContextAccessor>();
        accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

        return new UserRegistrationCheckoutHandler(
            userManager.Object,
            signInManager.Object,
            accessor.Object,
            CheckoutTestHelpers.CreatePaymentSessionCache(),
            Mock.Of<IDataProtectionProvider>(),
            Mock.Of<ISiteService>(),
            Options.Create(new DocumentJsonSerializerOptions()),
            Mock.Of<IStringLocalizer<UserRegistrationCheckoutHandler>>());
    }
}
