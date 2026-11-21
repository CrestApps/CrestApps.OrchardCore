using CrestApps.OrchardCore.Accounts;
using CrestApps.OrchardCore.Accounts.Core.Indexes;
using CrestApps.OrchardCore.Accounts.Core.Services;
using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Learning.Commerce;
using CrestApps.OrchardCore.Learning.Commerce.Core;
using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Commerce.Services;
using CrestApps.OrchardCore.Learning.Models;
using CrestApps.OrchardCore.Learning.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Locking.Distributed;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// Review round 6, Phase 5 findings 4 to 7 and the low item on two seats: a held seat comes back when its invitation is
/// taken back, replaced or expires; two gives at once never exceed the pool; a failed enrollment gives the seat back;
/// two creates for one order line make one pool; and a person who already holds a seat keeps only that one.
/// </summary>
public sealed class LicensePoolRound6Tests
{
    [Fact]
    public async Task AHeldSeat_ComesBack_WhenItsInvitationIsTakenBack_OrExpires_Once()
    {
        // Arrange
        var (h, pool) = await CreateAsync(seats: 2);
        await using var _ = h;

        var first = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().InviteAsync(pool.ItemId, "one@example.com", "boss"));
        var second = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().InviteAsync(pool.ItemId, "two@example.com", "boss"));
        var whileHeld = await RemainingAsync(h, pool);

        // Act: the first invitation is taken back twice; the second expires.
        await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().RevokeAsync(first.Seat.InvitationId, "boss"));
        await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().RevokeAsync(first.Seat.InvitationId, "boss"));
        var afterRevoke = await RemainingAsync(h, pool);

        Clock.UtcNow = Clock.UtcNow.AddDays(15);
        var expired = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().ExpireDueAsync());
        var afterExpiry = await RemainingAsync(h, pool);
        var seats = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListSeatsAsync(pool.ItemId));

        // Assert
        Assert.True(first.Succeeded, first.Error);
        Assert.True(second.Succeeded, second.Error);
        Assert.Equal(0, whileHeld);
        Assert.Equal(1, afterRevoke);
        Assert.Equal(1, expired);
        Assert.Equal(2, afterExpiry);
        Assert.All(seats, seat => Assert.Equal(LicenseSeatState.Removed, seat.State));
    }

    [Fact]
    public async Task TwoGivesAtOnce_WithOneSeatLeft_GiveOneSeat()
    {
        // Arrange
        var (h, pool) = await CreateAsync(seats: 1);
        await using var _ = h;

        // Act
        var results = await Task.WhenAll(
            h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ana", "boss")),
            h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ben", "boss")));
        var remaining = await RemainingAsync(h, pool);

        // Assert
        Assert.Single(results, result => result.Succeeded);
        Assert.Equal(0, remaining);
    }

    [Fact]
    public async Task AFailedEnrollment_GivesTheSeatBack()
    {
        // Arrange: the course needs another one first, which ana has not completed.
        var (h, pool) = await CreateAsync(seats: 1, configureCourse: part => part.Prerequisites = [new CoursePrerequisite { Kind = "Course", Id = "other" }]);
        await using var _ = h;

        // Act
        var result = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ana", "boss"));
        var remaining = await RemainingAsync(h, pool);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal(1, remaining);
    }

    [Fact]
    public async Task TwoCreatesForOneOrderLine_MakeOnePool()
    {
        // Arrange
        var (h, _) = await CreateAsync(seats: 1);
        await using var harness = h;

        // Act
        var pools = await Task.WhenAll(
            h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Seats = 5, OrderId = "o2", LineId = "l1" })),
            h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Seats = 5, OrderId = "o2", LineId = "l1" })));
        var listed = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListForAccountAsync("acme"));

        // Assert
        Assert.Equal(pools[0].ItemId, pools[1].ItemId);
        Assert.Single(listed, pool => pool.OrderId == "o2");
        Assert.Equal(5, await RemainingAsync(h, pools[0]));
    }

    [Fact]
    public async Task APersonWhoHoldsASeat_KeepsOnlyThatOne_WhenTheyAcceptAnInvitationToTheSamePool()
    {
        // Arrange: "new" is invited, then given a seat directly once they joined another way.
        var (h, pool) = await CreateAsync(seats: 3);
        await using var _ = h;

        var invited = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().InviteAsync(pool.ItemId, "new@example.com", "boss"));

        Accounts["acme"].Alter<AccountMembersPart>(part => part.Members.Add(new AccountMember { UserId = "new", UserName = "new", Email = "other-new@example.com" }));
        var direct = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "new", "boss"));
        Accounts["acme"].Alter<AccountMembersPart>(part => part.Members.RemoveAll(member => member.UserId == "new"));

        // Act
        var accepted = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(Tokens.Last(), "new"));
        var seats = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListSeatsAsync(pool.ItemId));

        // Assert
        Assert.True(invited.Succeeded, invited.Error);
        Assert.True(direct.Succeeded, direct.Error);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Single(seats, seat => seat.UserId == "new" && seat.State == LicenseSeatState.Assigned);
        Assert.Equal(2, await RemainingAsync(h, pool));
    }

    private static readonly Dictionary<string, ContentItem> Accounts = new(StringComparer.Ordinal);

    private static readonly List<string> Tokens = [];

    private static readonly CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock Clock = new() { UtcNow = LearningTestHarness.Now };

    private static async Task<(CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness Harness, LicensePool Pool)> CreateAsync(int seats, Action<CoursePart> configureCourse = null)
    {
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account", DisplayText = "Acme", Latest = true, Published = true };

        account.Apply(new AccountMembersPart
        {
            Members =
            [
                new AccountMember { UserId = "boss", UserName = "boss", Email = "boss@example.com", Role = AccountMemberRole.Admin },
                new AccountMember { UserId = "ana", UserName = "ana", Email = "ana@example.com" },
                new AccountMember { UserId = "ben", UserName = "ben", Email = "ben@example.com" },
            ],
        });
        Accounts["acme"] = account;
        Clock.UtcNow = LearningTestHarness.Now;

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                var content = new Mock<IContentManager>();

                content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(() => Accounts["acme"]);
                services.RemoveAll<global::OrchardCore.Modules.IClock>();
                services.AddSingleton<global::OrchardCore.Modules.IClock>(Clock);
                services.RemoveAll<IContentManager>();
                services.AddSingleton(content.Object);
                services.RemoveAll<IDistributedLock>();
                services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
                services.AddSingleton(Users());
                services.AddSingleton(Mock.Of<IAccountMemberService>());
                services.AddScoped<AccountInvitationStore>();
                services.AddScoped<IAccountInvitationService, DefaultAccountInvitationService>();
                services.AddLearningCommerceCore();
                services.AddSingleton<IAccountInvitationObserver, TokenCapture>();
            },
            async schemaBuilder =>
            {
                await LearningCommerceIndexSchema.CreateAsync(schemaBuilder);
                await AccountInvitationIndexSchema.CreateAsync(schemaBuilder);
            },
            [.. LearningCommerceIndexProviders.All(), new AccountInvitationIndexProvider()],
            [LearningCommerceConstants.CollectionName, LearningCommerceConstants.SeatLedgerCollectionName, AccountsConstants.InvitationsCollectionName]);

        courses.AddCourse("c1", configureCourse);
        courses.AddItem("c1", "lesson");
        courses.AddCourse("other");
        courses.AddItem("other", "other-lesson");

        var pool = await harness.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Title = "Safety", Seats = seats, OrderId = "o1", LineId = "l1" }));

        return (harness, pool);
    }

    private static Task<int> RemainingAsync(CrestApps.OrchardCore.Tests.Calendars.CalendarTestHarness h, LicensePool pool)
        => h.RunAsync(async services =>
        {
            var service = services.GetRequiredService<ILicensePoolService>();

            return await service.GetRemainingAsync(await service.FindAsync(pool.ItemId));
        });

    private sealed class TokenCapture : IAccountInvitationObserver
    {
        public Task InvitedAsync(AccountInvitation invitation, string token, CancellationToken cancellationToken = default)
        {
            lock (Tokens)
            {
                Tokens.Add(token);
            }

            return Task.CompletedTask;
        }
    }

    private static UserManager<IUser> Users()
    {
        var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);

        foreach (var name in new[] { "boss", "ana", "ben", "new" })
        {
            var user = Mock.Of<IUser>(value => value.UserName == name);

            users.Setup(manager => manager.FindByIdAsync(name)).ReturnsAsync(user);
            users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync(name + "@example.com");
            users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync(name);
            users.Setup(manager => manager.IsEmailConfirmedAsync(user)).ReturnsAsync(true);
        }

        return users.Object;
    }
}
