using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Json;
using OrchardCore.Settings;
using OrchardCore.Users;
using OrchardCore.Users.Models;

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

    /// <summary>
    /// The account a checkout creates is signed in by the account itself once the checkout completes; the password
    /// is never kept on the request.
    /// </summary>
    [Fact]
    public async Task CompletedAsync_SignsInTheCreatedAccount_WithoutItsPassword()
    {
        // Arrange
        var httpContext = new DefaultHttpContext();
        var registration = new Registration(httpContext);
        var session = await registration.CreatePendingSessionAsync("session-1");
        var flow = new CheckoutFlow(session);

        // Act
        await registration.Handler.CompletingAsync(new CheckoutFlowCompletingContext(flow));
        await registration.Handler.CompletedAsync(new CheckoutFlowCompletedContext(flow));

        // Assert
        Assert.Equal("user-1", session.OwnerId);
        Assert.DoesNotContain(httpContext.Features, feature => feature.Value?.GetType().Namespace?.StartsWith("CrestApps", StringComparison.Ordinal) == true);
        registration.SignInManager.Verify(manager => manager.SignInAsync(It.Is<IUser>(user => ((User)user).UserId == "user-1"), false, null), Times.Once);
        registration.SignInManager.Verify(manager => manager.PasswordSignInAsync(It.IsAny<IUser>(), It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
        Assert.Null(await registration.Cache.GetAsync<string>("session-1", UserRegistrationCheckoutHandler.PasswordPurpose));
    }

    /// <summary>
    /// A checkout that fails deletes the account it created, found by its id.
    /// </summary>
    [Fact]
    public async Task FailedAsync_DeletesTheAccountTheCheckoutCreated()
    {
        // Arrange
        var registration = new Registration(new DefaultHttpContext());
        var session = await registration.CreatePendingSessionAsync("session-1");
        var flow = new CheckoutFlow(session);

        // Act
        await registration.Handler.CompletingAsync(new CheckoutFlowCompletingContext(flow));
        await registration.Handler.FailedAsync(new CheckoutFlowFailedContext(flow));

        // Assert
        registration.UserManager.Verify(manager => manager.FindByIdAsync("user-1"), Times.Once);
        registration.UserManager.Verify(manager => manager.DeleteAsync(It.Is<IUser>(user => ((User)user).UserId == "user-1")), Times.Once);
        registration.SignInManager.Verify(manager => manager.SignInAsync(It.IsAny<IUser>(), It.IsAny<bool>(), It.IsAny<string>()), Times.Never);
    }

    /// <summary>
    /// A checkout that created no account deletes nothing, even when another checkout in the same scope did.
    /// </summary>
    [Fact]
    public async Task FailedAsync_ForACheckoutThatCreatedNoAccount_DeletesNothing()
    {
        // Arrange
        var registration = new Registration(httpContext: null);
        var created = await registration.CreatePendingSessionAsync("session-1");

        await registration.Handler.CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(created)));

        var other = CreateSession(ownerId: null);
        other.SessionId = "session-2";

        // Act
        await registration.Handler.FailedAsync(new CheckoutFlowFailedContext(new CheckoutFlow(other)));

        // Assert
        registration.UserManager.Verify(manager => manager.DeleteAsync(It.IsAny<IUser>()), Times.Never);
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

    private sealed class Registration
    {
        private readonly EphemeralDataProtectionProvider _protection = new();
        private readonly User _created = new() { UserName = "subscriber", Email = "subscriber@example.com" };

        public Registration(HttpContext httpContext)
        {
            UserManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            UserManager
                .Setup(manager => manager.CreateAsync(It.IsAny<IUser>(), "Password!1"))
                .Callback<IUser, string>((user, password) => ((User)user).UserId = "user-1")
                .ReturnsAsync(IdentityResult.Success);
            UserManager
                .Setup(manager => manager.FindByIdAsync("user-1"))
                .ReturnsAsync(() => new User { UserId = "user-1", UserName = "subscriber" });
            UserManager
                .Setup(manager => manager.DeleteAsync(It.IsAny<IUser>()))
                .ReturnsAsync(IdentityResult.Success);

            SignInManager = new Mock<SignInManager<IUser>>(
                UserManager.Object,
                Mock.Of<IHttpContextAccessor>(),
                Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
                null,
                null,
                null,
                null);
            SignInManager
                .Setup(manager => manager.CanSignInAsync(It.IsAny<IUser>()))
                .ReturnsAsync(true);

            var accessor = new Mock<IHttpContextAccessor>();
            accessor.SetupGet(a => a.HttpContext).Returns(httpContext);

            Cache = CheckoutTestHelpers.CreatePaymentSessionCache();

            Handler = new UserRegistrationCheckoutHandler(
                UserManager.Object,
                SignInManager.Object,
                accessor.Object,
                Cache,
                _protection,
                SiteServiceFactory.Create(new SubscriptionRoleSettings()),
                Options.Create(new DocumentJsonSerializerOptions()),
                Mock.Of<IStringLocalizer<UserRegistrationCheckoutHandler>>());
        }

        public Mock<UserManager<IUser>> UserManager { get; }

        public Mock<SignInManager<IUser>> SignInManager { get; }

        public PaymentSessionCache Cache { get; }

        public UserRegistrationCheckoutHandler Handler { get; }

        public async Task<CheckoutSession> CreatePendingSessionAsync(string sessionId)
        {
            var session = CreateSession(ownerId: null);
            session.SessionId = sessionId;
            session.SavedSteps[SubscriptionConstants.StepKey.UserRegistration] = JsonSerializer.SerializeToNode(
                new UserRegistrationStep { User = _created },
                new DocumentJsonSerializerOptions().SerializerOptions);

            var protectedPassword = _protection
                .CreateProtector(SubscriptionConstants.ProtectorPurposes.UserRegistrationStep)
                .Protect("Password!1");

            await Cache.SetAsync(sessionId, UserRegistrationCheckoutHandler.PasswordPurpose, protectedPassword);

            return session;
        }
    }
}
