using System.Text.Json;
using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Core.Services;
using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Core.Handlers;
using CrestApps.OrchardCore.Subscriptions.Core.Models;
using CrestApps.OrchardCore.Tests.Checkout;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Json;
using OrchardCore.Users;
using OrchardCore.Users.Models;
using YesSql;
using YesSql.Provider.Sqlite;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Tests.Subscriptions;

/// <summary>
/// The account a subscription checkout creates is written in the completion's own unit of work: Orchard Core's user
/// store flushes a new user without committing it, and the checkout engine cancels that unit of work when a later
/// completing handler fails. So a completion that fails after the account was created leaves no account behind, and
/// the retry, which loads the checkout without an owner, creates it once, without a duplicate user name.
/// </summary>
public sealed class UserRegistrationCheckoutRollbackTests
{
    private const string SessionId = "session-1";
    private const string Password = "Password!1";

    [Fact]
    public async Task ACompletionRolledBackAfterTheAccountWasCreated_LeavesNoAccount_AndTheRetryCreatesItOnce()
    {
        // Arrange
        var path = Path.Combine(Path.GetTempPath(), $"crestapps-registration-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={path};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        var protection = new EphemeralDataProtectionProvider();
        var cache = CheckoutTestHelpers.CreatePaymentSessionCache();

        await cache.SetAsync(
            SessionId,
            UserRegistrationCheckoutHandler.PasswordPurpose,
            protection.CreateProtector(SubscriptionConstants.ProtectorPurposes.UserRegistrationStep).Protect(Password));

        try
        {
            // Act: the first completion creates the account, then a later completing handler fails and the engine
            // cancels the unit of work.
            await using (var session = store.CreateSession())
            {
                var checkout = CreateCheckout();

                await CreateHandler(session, cache, protection).CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(checkout)));
                await session.CancelAsync();
            }

            int afterRollback;

            await using (var session = store.CreateSession())
            {
                afterRollback = await CountSubscribersAsync(session);
            }

            // The retry loads the checkout as it was committed: with no owner.
            var retried = CreateCheckout();

            await using (var session = store.CreateSession())
            {
                await CreateHandler(session, cache, protection).CompletingAsync(new CheckoutFlowCompletingContext(new CheckoutFlow(retried)));
                await session.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            int afterRetry;

            await using (var session = store.CreateSession())
            {
                afterRetry = await CountSubscribersAsync(session);
            }

            // Assert
            Assert.Equal(0, afterRollback);
            Assert.Equal(1, afterRetry);
            Assert.False(string.IsNullOrEmpty(retried.OwnerId));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, path);
        }
    }

    private static async Task<int> CountSubscribersAsync(ISession session)
        => (await session.Query<User>().ListAsync(TestContext.Current.CancellationToken)).Count(user => user.UserName == "subscriber");

    private static CheckoutSession CreateCheckout()
    {
        var checkout = new CheckoutSession
        {
            SessionId = SessionId,
            ReferenceType = SubscriptionCheckout.ReferenceType,
            Status = CheckoutSessionStatus.PaymentPending,
        };

        checkout.Steps.Add(new CheckoutFlowStep
        {
            Key = SubscriptionConstants.StepKey.UserRegistration,
            Order = 2,
            CollectData = true,
        });

        checkout.SavedSteps[SubscriptionConstants.StepKey.UserRegistration] = JsonSerializer.SerializeToNode(
            new UserRegistrationStep { User = new User { UserName = "subscriber", Email = "subscriber@example.com" } },
            new DocumentJsonSerializerOptions().SerializerOptions);

        return checkout;
    }

    // A user manager whose store does what Orchard Core's user store does on create: it refuses a user name that is
    // taken, then saves the user and flushes it into the session's transaction without committing it.
    private static UserRegistrationCheckoutHandler CreateHandler(ISession session, PaymentSessionCache cache, IDataProtectionProvider protection)
    {
        var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
        userManager
            .Setup(manager => manager.CreateAsync(It.IsAny<IUser>(), Password))
            .Returns(async (IUser user, string _) =>
            {
                var newUser = (User)user;

                if ((await session.Query<User>().ListAsync()).Any(existing => existing.UserName == newUser.UserName))
                {
                    return IdentityResult.Failed(new IdentityError { Code = "DuplicateUserName" });
                }

                newUser.UserId = Guid.NewGuid().ToString("N");

                await session.SaveAsync(newUser);
                await session.FlushAsync();

                return IdentityResult.Success;
            });

        var signInManager = new Mock<SignInManager<IUser>>(
            userManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<IUser>>(),
            null,
            null,
            null,
            null);

        return new UserRegistrationCheckoutHandler(
            userManager.Object,
            signInManager.Object,
            Mock.Of<IHttpContextAccessor>(),
            cache,
            protection,
            SiteServiceFactory.Create(new SubscriptionRoleSettings()),
            Options.Create(new DocumentJsonSerializerOptions()),
            Mock.Of<IStringLocalizer<UserRegistrationCheckoutHandler>>());
    }
}
