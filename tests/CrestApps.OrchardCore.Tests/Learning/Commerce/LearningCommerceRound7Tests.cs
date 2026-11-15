using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Learning.Commerce.Core;
using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Handlers;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Core.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.PersonalData;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Learning.Assessments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// Review round 7: a paid order commits nothing inside the checkout and is fulfilled afterwards, once, with failures
/// retried or recorded; a paid course enrolls the buyer even with an unmet prerequisite; the cart refuses a course the
/// buyer cannot take and caps a single-person course at one; a refund that meets a busy enrollment is not recorded as
/// done until access is gone; and erasing a person gives their seats back and clears them from purchases.
/// </summary>
public sealed class LearningCommerceRound7Tests
{
    [Fact]
    public async Task APaidOrder_CommitsNothingInsideTheCheckout_SoARolledBackCheckoutLeavesNoGrant()
    {
        // Arrange: review round 7, finding 1.
        var (h, order) = await CreateAsync();
        await using var _ = h;

        // Act: the checkout runs the order handlers, then fails before its own commit.
        var thrown = await Record.ExceptionAsync(() => h.RunAsync<object>(async services =>
        {
            await services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken);

            throw new InvalidOperationException("The checkout failed after the handlers ran.");
        }));
        await FulfillAsync(h);
        var enrollments = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));
        var pools = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme"));
        var purchases = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync(order.ItemId));

        // Assert
        Assert.NotNull(thrown);
        Assert.Empty(enrollments);
        Assert.Empty(pools);
        Assert.Empty(purchases);
    }

    [Fact]
    public async Task ASeatGrantThatFailsOnTheSecondLine_LeavesBothLinesPending_AndTheNextPassFulfilsBoth()
    {
        // Arrange: review round 7, finding 1. The seat line's account lookup throws once.
        var (h, order) = await CreateAsync(flakyAccount: true, seatsLine: true);
        await using var _ = h;

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        var enrollmentsAfterPaid = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));
        var purchasesAfterPaid = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync(order.ItemId));

        await FulfillAsync(h);
        var poolsAfterFirstPass = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme"));

        await FulfillAsync(h);
        var enrollments = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));
        var pools = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme"));

        // Assert
        Assert.Empty(enrollmentsAfterPaid);
        Assert.Equal(2, purchasesAfterPaid.Count);
        Assert.Empty(poolsAfterFirstPass);
        Assert.Single(enrollments, enrollment => enrollment.IsOpen);
        Assert.Equal(5, Assert.Single(pools).Seats);
    }

    [Fact]
    public async Task APaidOrderFulfilledTwice_GrantsOnce()
    {
        // Arrange: review round 7, finding 1. The paid event comes twice and the passes run more than once.
        var (h, order) = await CreateAsync(seatsLine: true);
        await using var _ = h;

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        await FulfillAsync(h);
        var enrollment = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1")));
        var pools = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme"));
        var purchases = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync(order.ItemId));

        // Assert
        Assert.Single(enrollment.Grants, grant => grant.Source == LearningCommerceConstants.PurchaseSource);
        Assert.Single(pools);
        Assert.Equal(2, purchases.Count);
    }

    [Fact]
    public async Task APaidCourse_EnrollsTheBuyer_EvenWithAnUnmetPrerequisite()
    {
        // Arrange: review round 7, finding 2. The buyer was allowed to pay, so the course is theirs.
        var (h, order) = await CreateAsync(prerequisite: true);
        await using var _ = h;

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        var enrollments = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));

        // Assert
        Assert.Single(enrollments, enrollment => enrollment.IsOpen);
    }

    [Fact]
    public async Task APaidCourseNoLongerAvailable_IsRecordedOnThePurchase_AndInAnOrderNote()
    {
        // Arrange: review round 7, finding 2. The course was taken down after it was put in the cart.
        var (h, order) = await CreateAsync(courseExists: false);
        await using var _ = h;

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        await FulfillAsync(h);
        var purchases = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync(order.ItemId));

        // Assert
        Assert.Single(purchases);
        Assert.Single(order.Notes ?? []);
    }

    [Fact]
    public async Task APaidCourseMeetingABusyEnrollment_IsRetried_UntilTheBuyerIsEnrolled()
    {
        // Arrange: review round 7, finding 2. Another enrollment of the buyer in the course is being made.
        var (h, order) = await CreateAsync();
        await using var _ = h;

        // Act
        await h.RunAsync(async services =>
        {
            var (locker, locked) = await services.GetRequiredService<IDistributedLock>().TryAcquireLockAsync("Learning:Enrollment:boss:c1:", TimeSpan.FromSeconds(1));

            Assert.True(locked);

            await using (locker)
            {
                await h.RunAsync(other => other.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
                await FulfillAsync(h);
            }

            return true;
        });
        var whileBusy = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));

        await FulfillAsync(h);
        var enrollments = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1"));

        // Assert
        Assert.Empty(whileBusy);
        Assert.Single(enrollments, enrollment => enrollment.IsOpen);
    }

    [Fact]
    public async Task SeatsBoughtByABuyerInNoAccount_AreRecordedOnThePurchase_AndInAnOrderNote()
    {
        // Arrange: review round 7, finding 2. The buyer left the account between the cart and the payment.
        var (h, order) = await CreateAsync(userId: "solo", seatsOnly: true);
        await using var _ = h;

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        var purchases = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync(order.ItemId));

        // Assert
        Assert.Single(purchases);
        Assert.Single(order.Notes ?? []);
    }

    [Fact]
    public async Task TheCart_RefusesACourseTheBuyerCannotTake()
    {
        // Arrange: review round 7, finding 2. An unmet prerequisite, then a course that is no longer published.
        var (withPrerequisite, _) = await CreateAsync(prerequisite: true);
        var (withoutCourse, _) = await CreateAsync(courseExists: false);
        await using var first = withPrerequisite;
        await using var second = withoutCourse;

        // Act
        var unmet = await EvaluateAsync(withPrerequisite, quantity: 1);
        var gone = await EvaluateAsync(withoutCourse, quantity: 1);

        // Assert
        Assert.NotNull(unmet.Problem);
        Assert.NotNull(gone.Problem);
    }

    [Fact]
    public async Task ASinglePersonCourse_CanBeBoughtOnce_ButSeatsInAnyQuantity()
    {
        // Arrange: review round 7, finding 3.
        var (h, _) = await CreateAsync();
        await using var harness = h;

        // Act
        var course = await EvaluateAsync(h, quantity: 3);
        var seats = await EvaluateAsync(h, quantity: 3, contentItemId: "team");

        // Assert
        Assert.Null(course.Problem);
        Assert.Equal(1, course.MaxQuantity);
        Assert.Equal(99, seats.MaxQuantity);
    }

    [Fact]
    public async Task ARefundMeetingABusyEnrollment_IsNotRecordedDone_UntilAccessIsGone()
    {
        // Arrange: review round 7, finding 4.
        var (h, order) = await CreateAsync();
        await using var _ = h;

        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        var enrollment = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1")));

        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = order.Total.Value });

        // Act: the learner is busy in the player while the refund arrives and while the next pass runs.
        await h.RunAsync(async services =>
        {
            var locks = services.GetRequiredService<IDistributedLock>();
            var (locker, locked) = await locks.TryAcquireLockAsync(EnrollmentWriteGate.Key(enrollment.ItemId), TimeSpan.FromSeconds(1));

            Assert.True(locked);

            await using (locker)
            {
                await h.RunAsync(other => other.GetRequiredService<LearningOrderEventHandler>().RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken));
                await FulfillAsync(h);
            }

            return true;
        });
        var whileBusy = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().FindAsync(order.ItemId, "l1"));
        var accessWhileBusy = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId));

        await FulfillAsync(h);
        var afterRetry = await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().FindByIdAsync(enrollment.ItemId));
        var purchase = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().FindAsync(order.ItemId, "l1"));

        // Assert
        Assert.True(accessWhileBusy.IsOpen);
        Assert.Null(whileBusy.RevokedUtc);
        Assert.False(afterRetry.IsOpen);
        Assert.NotNull(purchase.RevokedUtc);
    }

    [Fact]
    public async Task ErasingAPerson_GivesTheirAssignedAndInvitedSeatsBack()
    {
        // Arrange: review round 7, finding 5.
        var (h, _) = await CreateAsync();
        await using var harness = h;

        var pool = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Title = "Safety", Seats = 2, OrderId = "o9", LineId = "l1" }));
        await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ana", "boss"));
        await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().InviteAsync(pool.ItemId, "zoe@example.com", "boss"));

        // Act
        foreach (var subject in new[] { new PersonalDataSubject(userId: "ana", email: null), new PersonalDataSubject(userId: null, email: "zoe@example.com") })
        {
            await AnonymizeAsync(h, subject);
        }

        var remaining = await h.RunAsync(async services =>
        {
            var pools = services.GetRequiredService<ILicensePoolService>();

            return await pools.GetRemainingAsync(await pools.FindAsync(pool.ItemId));
        });
        var seats = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListSeatsAsync(pool.ItemId));

        // Assert
        Assert.Equal(2, remaining);
        Assert.All(seats, seat =>
        {
            Assert.Null(seat.UserId);
            Assert.Null(seat.Email);
        });
    }

    [Fact]
    public async Task ThePersonalDataOfABuyer_IncludesTheirPurchases_AndErasingThemClearsTheBuyer()
    {
        // Arrange: review round 7, low finding.
        var (h, order) = await CreateAsync();
        await using var _ = h;

        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await FulfillAsync(h);
        var subject = new PersonalDataSubject(userId: "boss", email: null);

        // Act
        var export = new PersonalDataExport();
        await h.RunAsync(async services =>
        {
            await ActivatorUtilities.CreateInstance<LearningCommercePersonalDataContributor>(services).ExportAsync(new PersonalDataExportContext(subject, export));

            return true;
        });
        await AnonymizeAsync(h, subject);
        var purchase = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().FindAsync(order.ItemId, "l1"));

        // Assert
        Assert.Single(export.Records, record => record.Kind == "LearningPurchase");
        Assert.Null(purchase.UserId);
    }

    // Runs what fulfils paid orders after the checkout, then moves the clock past the back-off so the next pass finds
    // the work that was retried.
    private static async Task FulfillAsync(CalendarTestHarness h)
    {
        await h.RunAsync(services => services.GetRequiredService<LearningPurchaseFulfillment>().RunDueAsync(TestContext.Current.CancellationToken), commit: false);

        var clock = (TestClock)await h.RunAsync(services => Task.FromResult(services.GetRequiredService<IClock>()), commit: false);

        clock.UtcNow = clock.UtcNow.AddHours(2);
    }

    private static async Task AnonymizeAsync(CalendarTestHarness h, PersonalDataSubject subject)
        => await h.RunAsync(async services =>
        {
            var contributor = ActivatorUtilities.CreateInstance<LearningCommercePersonalDataContributor>(services);

            await contributor.AnonymizeAsync(new PersonalDataAnonymizeContext(subject, null, new PersonalDataAnonymizeResult()));

            return true;
        });

    private static Task<CartLineEvaluateContext> EvaluateAsync(CalendarTestHarness h, int quantity, string contentItemId = "c1")
        => h.RunAsync(async services =>
        {
            var item = await services.GetRequiredService<IContentManager>().GetAsync(contentItemId, VersionOptions.Published);
            var handler = ActivatorUtilities.CreateInstance<LearningCartLineHandler>(services, new HttpContextAccessor { HttpContext = SignedIn("boss") }, new PassThrough());
            var context = new CartLineEvaluateContext { ContentItem = item, Quantity = quantity, MaxQuantity = 99 };

            await handler.EvaluateAsync(context, TestContext.Current.CancellationToken);

            return context;
        });

    private static async Task<(CalendarTestHarness Harness, Order Order)> CreateAsync(
        bool prerequisite = false,
        bool courseExists = true,
        bool flakyAccount = false,
        bool seatsLine = false,
        bool seatsOnly = false,
        string userId = "boss")
    {
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account", DisplayText = "Acme", Latest = true, Published = true };

        account.Apply(new AccountMembersPart
        {
            Members =
            [
                new AccountMember { UserId = "boss", UserName = "boss", Email = "boss@example.com", Role = AccountMemberRole.Admin },
                new AccountMember { UserId = "ana", UserName = "ana", Email = "ana@example.com" },
            ],
        });

        var course = new ContentItem { ContentItemId = "c1", ContentItemVersionId = "v1", ContentType = "Course", DisplayText = "Safety" };
        course.Apply(new LearningProductPart());

        var seats = new ContentItem { ContentItemId = "team", ContentItemVersionId = "v2", ContentType = "Product", DisplayText = "Safety for teams" };
        seats.Apply(new LearningProductPart { Grants = [new LearningProductGrant { Kind = LearningProductGrantKind.Seats, TargetId = "c1", Seats = 5 }] });

        var courseLine = new OrderLine { LineId = "l1", ContentItemId = "c1", ContentItemVersionId = "v1", Quantity = 1 };
        var seatsOrderLine = new OrderLine { LineId = "l2", ContentItemId = "team", ContentItemVersionId = "v2", Quantity = 1 };
        var order = new Order
        {
            ItemId = "o1",
            UserId = userId,
            Status = OrderStatus.Paid,
            Currency = "USD",
            Total = 100m,
            Lines = seatsOnly ? [seatsOrderLine] : seatsLine ? [courseLine, seatsOrderLine] : [courseLine],
        };

        var clock = new TestClock { UtcNow = CalendarTestHarness.Now };

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                var content = new Mock<IContentManager>();

                content.Setup(manager => manager.GetVersionAsync("v1")).ReturnsAsync(course);
                content.Setup(manager => manager.GetVersionAsync("v2")).ReturnsAsync(seats);
                content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(account);
                content.Setup(manager => manager.GetAsync("c1", It.IsAny<VersionOptions>())).ReturnsAsync(course);
                content.Setup(manager => manager.GetAsync("team", It.IsAny<VersionOptions>())).ReturnsAsync(seats);

                var members = new Mock<IAccountMemberService>();

                if (flakyAccount)
                {
                    members.SetupSequence(service => service.FindAccountAsync("boss", It.IsAny<CancellationToken>()))
                        .ThrowsAsync(new InvalidOperationException("The account store is not reachable."))
                        .ReturnsAsync(account)
                        .ReturnsAsync(account)
                        .ReturnsAsync(account);
                }
                else
                {
                    members.Setup(service => service.FindAccountAsync("boss", It.IsAny<CancellationToken>())).ReturnsAsync(account);
                }

                var invitations = new Mock<IAccountInvitationService>();

                invitations.Setup(service => service.InviteAsync(It.IsAny<AccountInvitationRequest>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync((AccountInvitationRequest request, CancellationToken _) => new AccountInvitationResult { Invitation = new AccountInvitation { ItemId = "inv-" + request.SourceId, Email = request.Email, Source = request.Source, SourceId = request.SourceId }, Token = "token" });

                var orderStore = new Mock<IOrderStore>();

                orderStore.Setup(store => store.FindByIdAsync(order.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(order);

                var orders = new Mock<IOrderService>();

                orders.Setup(service => service.AddNoteAsync(It.IsAny<Order>(), It.IsAny<string>(), It.IsAny<OrderNoteKind>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                    .Callback((Order target, string text, OrderNoteKind kind, string author, CancellationToken _) => (target.Notes ??= []).Add(new OrderNote { Text = text, Kind = kind, AuthorName = author }))
                    .Returns(Task.CompletedTask);

                services.RemoveAll<IContentManager>();
                services.AddSingleton(content.Object);
                services.AddSingleton(members.Object);
                services.AddSingleton(Users());
                services.AddSingleton(invitations.Object);
                services.AddSingleton(orderStore.Object);
                services.AddSingleton(orders.Object);
                services.RemoveAll<IDistributedLock>();
                services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
                services.RemoveAll<IClock>();
                services.AddSingleton<IClock>(clock);
                services.AddLearningCommerceCore();
                services.AddScoped<LearningOrderEventHandler>();
                services.AddScoped<LearningPurchaseFulfillment>();
                services.AddScoped<LearningPurchaseProcessor>();
                services.AddLocalization();
            },
            schemaBuilder => LearningCommerceIndexSchema.CreateAsync(schemaBuilder),
            LearningCommerceIndexProviders.All(),
            [LearningCommerceConstants.CollectionName, LearningCommerceConstants.SeatLedgerCollectionName, LearningCommerceConstants.FulfillmentWorkCollectionName]);

        courses.AddCourse("other");
        courses.AddItem("other", "other-lesson");

        if (courseExists)
        {
            courses.AddCourse("c1", part =>
            {
                if (prerequisite)
                {
                    part.Prerequisites = [new CoursePrerequisite { Kind = "Course", Id = "other" }];
                }
            });
            courses.AddItem("c1", "lesson");
        }

        return (harness, order);
    }

    private static DefaultHttpContext SignedIn(string userId)
        => new DefaultHttpContext { User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, userId)], "test")) };

    private static UserManager<IUser> Users()
    {
        var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        foreach (var name in new[] { "boss", "ana" })
        {
            var user = Mock.Of<IUser>(value => value.UserName == name);

            users.Setup(manager => manager.FindByIdAsync(name)).ReturnsAsync(user);
            users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync(name + "@example.com");
            users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync(name);
        }

        return users.Object;
    }

    private sealed class PassThrough : IStringLocalizer<LearningCartLineHandler>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
