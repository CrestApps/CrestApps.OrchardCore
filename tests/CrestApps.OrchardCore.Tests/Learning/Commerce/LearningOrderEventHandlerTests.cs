using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Learning.Commerce.Core;
using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Core.Services;
using CrestApps.OrchardCore.Learning.Commerce.Handlers;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// A paid order enrolls the buyer in the course they bought and gives their account a pool of seats; paying twice
/// grants nothing more, a partial refund keeps it all, and a full refund takes the enrollment and the pool back.
/// </summary>
public sealed class LearningOrderEventHandlerTests
{
    [Fact]
    public async Task APaidOrder_EnrollsTheBuyer_AndGivesTheAccountSeats_AndAFullRefundTakesBothBack()
    {
        // Arrange
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account", DisplayText = "Acme", Latest = true, Published = true };

        account.Apply(new AccountMembersPart
        {
            Members = [new AccountMember { UserId = "boss", UserName = "boss", Email = "boss@example.com", Role = AccountMemberRole.Admin }],
        });

        var course = new ContentItem { ContentItemId = "c1", ContentItemVersionId = "v1", ContentType = "Course", DisplayText = "Safety" };
        course.Apply(new LearningProductPart());

        var seats = new ContentItem { ContentItemId = "team", ContentItemVersionId = "v2", ContentType = "Product", DisplayText = "Safety for teams" };
        seats.Apply(new LearningProductPart { Grants = [new LearningProductGrant { Kind = LearningProductGrantKind.Seats, TargetId = "c1", Seats = 5 }] });

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                var content = new Mock<IContentManager>();

                content.Setup(manager => manager.GetVersionAsync("v1")).ReturnsAsync(course);
                content.Setup(manager => manager.GetVersionAsync("v2")).ReturnsAsync(seats);
                content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(account);
                content.Setup(manager => manager.GetAsync("c1", It.IsAny<VersionOptions>())).ReturnsAsync(course);

                var members = new Mock<IAccountMemberService>();

                members.Setup(service => service.FindAccountAsync("boss", It.IsAny<CancellationToken>())).ReturnsAsync(account);

                services.RemoveAll<IContentManager>();
                services.AddSingleton(content.Object);
                services.AddSingleton(members.Object);
                services.AddSingleton(Users());
                services.AddSingleton(Mock.Of<IAccountInvitationService>());
                services.AddLearningCommerceCore();
                services.AddScoped<LearningOrderEventHandler>();
            },
            schemaBuilder => LearningCommerceIndexSchema.CreateAsync(schemaBuilder),
            LearningCommerceIndexProviders.All(),
            [LearningCommerceConstants.CollectionName, LearningCommerceConstants.SeatLedgerCollectionName]);

        await using var h = harness;

        courses.AddCourse("c1");
        courses.AddItem("c1", "lesson");

        var order = new Order
        {
            ItemId = "o1",
            UserId = "boss",
            Status = OrderStatus.Paid,
            Currency = "USD",
            Total = 100m,
            Lines =
            [
                new OrderLine { LineId = "l1", ContentItemId = "c1", ContentItemVersionId = "v1", Quantity = 1 },
                new OrderLine { LineId = "l2", ContentItemId = "team", ContentItemVersionId = "v2", Quantity = 2 },
            ],
        };

        // Act
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().PaidAsync(new OrderPaidContext(order), TestContext.Current.CancellationToken));

        var enrollment = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1")));
        var pool = Assert.Single(await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme")));
        var purchases = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync("o1"));

        order.Refunds.Add(new OrderRefund { RefundId = "r1", Amount = 40m });
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().RefundedAsync(new OrderRefundedContext(order, order.Refunds[0]), TestContext.Current.CancellationToken));
        var afterPartial = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1")));

        order.Refunds.Add(new OrderRefund { RefundId = "r2", Amount = 60m });
        await h.RunAsync(services => services.GetRequiredService<LearningOrderEventHandler>().RefundedAsync(new OrderRefundedContext(order, order.Refunds[1]), TestContext.Current.CancellationToken));
        var afterFull = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("boss", "c1")));
        var poolAfterFull = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().FindAsync(pool.ItemId));
        var purchasesAfterFull = await h.RunAsync(services => services.GetRequiredService<LearningPurchaseStore>().ListByOrderAsync("o1"));

        // Assert
        Assert.Equal(EnrollmentStatus.Active, enrollment.Status);
        Assert.Contains(enrollment.Grants, grant => grant.Source == LearningCommerceConstants.PurchaseSource && grant.SourceId == "o1:l1");
        Assert.Equal(10, pool.Seats);
        Assert.Equal("c1", pool.CourseId);
        Assert.Equal("Safety", pool.Title);
        Assert.Equal(2, purchases.Count);
        Assert.Equal(EnrollmentStatus.Active, afterPartial.Status);
        Assert.NotEqual(EnrollmentStatus.Active, afterFull.Status);
        Assert.Equal(LicensePoolState.Revoked, poolAfterFull.State);
        Assert.All(purchasesAfterFull, purchase => Assert.NotNull(purchase.RevokedUtc));
    }

    private static UserManager<IUser> Users()
    {
        var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);
        var user = Mock.Of<IUser>(value => value.UserName == "boss");

        users.Setup(manager => manager.FindByIdAsync("boss")).ReturnsAsync(user);
        users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync("boss@example.com");
        users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync("boss");

        return users.Object;
    }
}
