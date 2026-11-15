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
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Learning.Commerce;

/// <summary>
/// Licence pools: an order line creates its pool once, a seat goes only to a member and once per person and enrolls
/// them, an invited person gets the held seat when they accept, taking a seat back ends its enrollment and returns the
/// seat, and revoking the pool on a refund ends every enrollment from it.
/// </summary>
public sealed class LicensePoolServiceTests
{
    [Fact]
    public async Task Seats_GoToMembersOnce_InvitedPeopleGetThemOnAccepting_AndARefundTakesThemAllBack()
    {
        // Arrange
        var account = new ContentItem { ContentItemId = "acme", ContentType = "Account", DisplayText = "Acme", Latest = true, Published = true };

        account.Apply(new AccountMembersPart
        {
            Members =
            [
                new AccountMember { UserId = "boss", UserName = "boss", Email = "boss@example.com", Role = AccountMemberRole.Admin },
                new AccountMember { UserId = "ana", UserName = "ana", Email = "ana@example.com" },
            ],
        });

        var (harness, courses, _) = await LearningTestHarness.CreateAsync(
            services =>
            {
                var content = new Mock<IContentManager>();

                content.Setup(manager => manager.GetAsync("acme", It.IsAny<VersionOptions>())).ReturnsAsync(account);
                services.RemoveAll<IContentManager>();
                services.AddSingleton(content.Object);
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

        await using var h = harness;

        courses.AddCourse("c1");
        courses.AddItem("c1", "lesson");

        // Act
        var pool = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Title = "Safety", Seats = 3, OrderId = "o1", LineId = "l1" }));
        var sameLine = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().CreateAsync(new LicensePool { AccountId = "acme", CourseId = "c1", Seats = 3, OrderId = "o1", LineId = "l1" }));
        var toAna = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ana", "boss"));
        var toAnaAgain = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "ana", "boss"));
        var toStranger = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GiveAsync(pool.ItemId, "zoe", "boss"));
        var invited = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().InviteAsync(pool.ItemId, "new@example.com", "boss"));
        var remainingAfterInvite = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GetRemainingAsync(pool));

        var invitation = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().FindByIdAsync(invited.Seat.InvitationId));
        var accepted = await h.RunAsync(services => services.GetRequiredService<IAccountInvitationService>().AcceptAsync(Tokens.Last(), "new"));
        var newSeat = (await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListSeatsAsync(pool.ItemId))).Single(seat => seat.ItemId == invited.Seat.ItemId);
        var newEnrollment = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("new", "c1")));

        var removed = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().RemoveAsync(toAna.Seat.ItemId, "boss"));
        var remainingAfterRemove = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().GetRemainingAsync(pool));
        var anaAfterRemove = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("ana", "c1")));

        var revoked = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().RevokeAsync(pool.ItemId, "Refunded"));
        var newAfterRevoke = Assert.Single(await h.RunAsync(services => services.GetRequiredService<IEnrollmentStore>().ListByCourseForUserAsync("new", "c1")));
        var seatsAfterRevoke = await h.RunAsync(services => services.GetRequiredService<ILicensePoolService>().ListSeatsAsync(pool.ItemId));

        // Assert
        Assert.Equal(pool.ItemId, sameLine.ItemId);
        Assert.True(toAna.Succeeded, toAna.Error);
        Assert.False(toAnaAgain.Succeeded);
        Assert.False(toStranger.Succeeded);
        Assert.True(invited.Succeeded, invited.Error);
        Assert.Equal(1, remainingAfterInvite);
        Assert.Equal(LearningCommerceConstants.SeatSource, invitation.Source);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(LicenseSeatState.Assigned, newSeat.State);
        Assert.Equal("new", newSeat.UserId);
        Assert.Equal(EnrollmentStatus.Active, newEnrollment.Status);
        Assert.True(removed.Succeeded, removed.Error);
        Assert.Equal(2, remainingAfterRemove);
        Assert.NotEqual(EnrollmentStatus.Active, anaAfterRemove.Status);
        Assert.Equal(LicensePoolState.Revoked, revoked.State);
        Assert.NotEqual(EnrollmentStatus.Active, newAfterRevoke.Status);
        Assert.All(seatsAfterRevoke, seat => Assert.Equal(LicenseSeatState.Removed, seat.State));
    }

    private static readonly List<string> Tokens = [];

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

        foreach (var name in new[] { "boss", "ana", "new" })
        {
            var user = Mock.Of<IUser>(value => value.UserName == name);

            users.Setup(manager => manager.FindByIdAsync(name)).ReturnsAsync(user);
            users.Setup(manager => manager.GetEmailAsync(user)).ReturnsAsync(name + "@example.com");
            users.Setup(manager => manager.GetUserNameAsync(user)).ReturnsAsync(name);
        }

        return users.Object;
    }
}
