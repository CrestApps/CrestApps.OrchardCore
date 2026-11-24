using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Core.Services;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.Portal;
using CrestApps.OrchardCore.Tickets.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// What a requester sees in the portal, on a real store: their own requests, never another person's; an administrator
/// of an account also sees the account's requests, a plain member doesn't; and a guest's signed link opens only the
/// request it was made for.
/// </summary>
public sealed class TicketPortalTests
{
    [Fact]
    public async Task ARequesterSeesTheirOwnRequests_AndNotAnotherPersons()
    {
        // Arrange
        await using var kit = await CreateAsync(adminOf: null);
        var anas = await CreateAsync(kit, "u-ana", accountId: "acme");
        var bens = await CreateAsync(kit, "u-ben", accountId: "acme");

        // Act
        var result = await kit.ReadAsync(async services =>
        {
            var access = services.GetRequiredService<TicketPortalAccess>();
            var ana = await access.GetViewerAsync(CalendarTestHarness.User("u-ana"), TestContext.Current.CancellationToken);
            var (mine, total) = await access.ListAsync(ana, organization: false, 0, 20, TestContext.Current.CancellationToken);

            return new
            {
                Ana = ana,
                Mine = mine.Select(ticket => ticket.ContentItemId).ToList(),
                Total = total,
                SeesOwn = await access.CanSeeAsync(ana, await services.GetRequiredService<ITicketService>().GetAsync(anas), TestContext.Current.CancellationToken),
                SeesBens = await access.CanSeeAsync(ana, await services.GetRequiredService<ITicketService>().GetAsync(bens), TestContext.Current.CancellationToken),
            };
        });

        // Assert
        Assert.Equal([anas], result.Mine);
        Assert.Equal(1, result.Total);
        Assert.True(result.SeesOwn);
        Assert.False(result.SeesBens);
        Assert.Null(result.Ana.AdminAccountId);
    }

    [Fact]
    public async Task AnAccountAdministrator_AlsoSeesTheAccountsRequests()
    {
        // Arrange
        await using var kit = await CreateAsync(adminOf: "u-ana");
        await CreateAsync(kit, "u-ana", accountId: "acme");
        var bens = await CreateAsync(kit, "u-ben", accountId: "acme");
        await CreateAsync(kit, "u-cy", accountId: "other");

        // Act
        var result = await kit.ReadAsync(async services =>
        {
            var access = services.GetRequiredService<TicketPortalAccess>();
            var ana = await access.GetViewerAsync(CalendarTestHarness.User("u-ana"), TestContext.Current.CancellationToken);
            var ben = await access.GetViewerAsync(CalendarTestHarness.User("u-ben"), TestContext.Current.CancellationToken);
            var (organization, _) = await access.ListAsync(ana, organization: true, 0, 20, TestContext.Current.CancellationToken);

            return new
            {
                Ana = ana,
                Ben = ben,
                Organization = organization.Count,
                AnaSeesBens = await access.CanSeeAsync(ana, await services.GetRequiredService<ITicketService>().GetAsync(bens), TestContext.Current.CancellationToken),
            };
        });

        // Assert
        Assert.Equal("acme", result.Ana.AdminAccountId);
        Assert.Equal(2, result.Organization);
        Assert.True(result.AnaSeesBens);
        Assert.Null(result.Ben.AdminAccountId);
    }

    [Fact]
    public void AGuestsLink_OpensOnlyItsRequest_AndStopsAfterARequesterChangeAMergeOrItsTime()
    {
        // Arrange
        var now = new DateTime(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(() => now);
        var tokens = new SignedTokenService(new EphemeralDataProtectionProvider(), clock.Object);
        var access = new TicketPortalAccess(Mock.Of<ISession>(), [], tokens, Mock.Of<CrestApps.Core.Services.ICatalog<TicketDesk>>(), new ServiceCollection().BuildServiceProvider());
        var ticket = Guest("t1", "Dana@Contoso.com");
        var token = access.CreateGuestToken(ticket);
        var rating = access.CreateRatingToken(ticket);

        // Act
        var own = access.IsGuestTokenFor(token, ticket);
        var sameAddressOtherCase = access.IsGuestTokenFor(token, Guest("t1", "dana@contoso.com"));
        var other = access.IsGuestTokenFor(token, Guest("t2", "dana@contoso.com"));
        var forged = access.IsGuestTokenFor("not-a-token", ticket);
        var asRating = access.IsRatingTokenFor(token, ticket);
        var ratingOwn = access.IsRatingTokenFor(rating, ticket);
        var newRequester = access.IsGuestTokenFor(token, Guest("t1", "lee@contoso.com"));
        var erased = access.IsGuestTokenFor(token, Guest("t1", null));
        var merged = Guest("t1", "dana@contoso.com");
        merged.Alter<TicketPart>(part => part.AccessVersion++);
        var afterMerge = access.IsGuestTokenFor(token, merged);
        var ratingAfterMerge = access.IsRatingTokenFor(rating, merged);
        now = now.Add(TicketPortalAccess.GuestLinkLifetime).AddMinutes(1);
        var expired = access.IsGuestTokenFor(token, ticket);

        // Assert
        Assert.True(own);
        Assert.True(sameAddressOtherCase);
        Assert.False(other);
        Assert.False(forged);
        Assert.False(asRating);
        Assert.True(ratingOwn);
        Assert.False(newRequester);
        Assert.False(erased);
        Assert.False(afterMerge);
        Assert.False(ratingAfterMerge);
        Assert.False(expired);
    }

    [Theory]
    [InlineData(TicketDeskAudience.Customers, "u-ana", false, true)]
    [InlineData(TicketDeskAudience.Customers, "u-ben", false, true)]
    [InlineData(TicketDeskAudience.Internal, "u-ana", false, false)]
    [InlineData(TicketDeskAudience.Internal, "u-ben", false, false)]
    [InlineData(TicketDeskAudience.Internal, "u-ben", true, true)]
    public void ATicketOnAnInternalDesk_ShowsOnlyToItsRequester_WhenStaff_NeverThroughTheAccount(TicketDeskAudience audience, string viewerId, bool staff, bool sees)
    {
        // Arrange: Ana administers Acme; Ben, a member, asked.
        var ticket = new ContentItem { ContentItemId = "t1" };
        ticket.Apply(new TicketPart { DeskId = "d", AccountId = "acme", Requester = new PartyRef(PartyKinds.User, "u-ben") });
        var viewer = new TicketPortalViewer(viewerId, viewerId, new PartyRef(PartyKinds.User, viewerId), viewerId == "u-ana" ? "acme" : null, "Acme") { IsStaff = staff };

        // Act and assert: reading follows the same rule as what the portal offers to create on.
        Assert.Equal(sees, TicketPortalAccess.CanSee(viewer, ticket, audience));
        Assert.Equal(audience == TicketDeskAudience.Customers || staff, TicketPortalAccess.Offers(new TicketDesk { Audience = audience }, staff));
    }

    [Fact]
    public async Task TheList_LeavesOutInternalDesks_UnlessOnesOwnAndStaff()
    {
        // Arrange: the kit's one desk serves people inside the organization.
        await using var kit = await TicketKit.CreateAsync(configureDesk: desk => desk.Audience = TicketDeskAudience.Internal, configureServices: services =>
        {
            Portal(services, adminOf: "u-ana");
            var staff = new Mock<CrestApps.OrchardCore.Staff.Services.IStaffDirectory>();
            staff.Setup(value => value.IsStaffAsync("u-ben", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            services.AddSingleton(staff.Object);
        });
        await CreateAsync(kit, "u-ana", accountId: "acme");
        await CreateAsync(kit, "u-ben", accountId: "acme");

        // Act
        var result = await kit.ReadAsync(async services =>
        {
            var access = services.GetRequiredService<TicketPortalAccess>();
            var ana = await access.GetViewerAsync(CalendarTestHarness.User("u-ana"), TestContext.Current.CancellationToken);
            var ben = await access.GetViewerAsync(CalendarTestHarness.User("u-ben"), TestContext.Current.CancellationToken);

            return new
            {
                AnasOwn = (await access.ListAsync(ana, organization: false, 0, 20, TestContext.Current.CancellationToken)).Total,
                AnasAccount = (await access.ListAsync(ana, organization: true, 0, 20, TestContext.Current.CancellationToken)).Total,
                BensOwn = (await access.ListAsync(ben, organization: false, 0, 20, TestContext.Current.CancellationToken)).Total,
            };
        });

        // Assert: Ana isn't staff and the account never shows internal tickets; Ben is staff and sees his own.
        Assert.Equal(0, result.AnasOwn);
        Assert.Equal(0, result.AnasAccount);
        Assert.Equal(1, result.BensOwn);
    }

    private static ContentItem Guest(string id, string email)
    {
        var ticket = new ContentItem { ContentItemId = id };
        ticket.Apply(new TicketPart { RequesterContact = email is null ? null : new ContactInfo { Name = "Dana", Email = email } });

        return ticket;
    }

    private static Task<TicketKit> CreateAsync(string adminOf)
        => TicketKit.CreateAsync(configureServices: services => Portal(services, adminOf));

    private static void Portal(IServiceCollection services, string adminOf)
    {
        {
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            services.AddSingleton(provider => new SignedTokenService(provider.GetRequiredService<IDataProtectionProvider>(), provider.GetRequiredService<IClock>()));
            services.AddScoped<TicketPortalAccess>();

            var members = new Mock<IAccountMemberService>();
            members.Setup(service => service.FindAccountAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, CancellationToken _) =>
                {
                    var account = new ContentItem { ContentItemId = "acme", DisplayText = "Acme" };
                    account.Apply(new AccountMembersPart
                    {
                        Members =
                        [
                            new AccountMember { UserId = "u-ana", Role = adminOf == "u-ana" ? AccountMemberRole.Admin : AccountMemberRole.Member },
                            new AccountMember { UserId = "u-ben", Role = AccountMemberRole.Member },
                        ],
                    });

                    return userId is "u-ana" or "u-ben" ? account : null;
                });
            services.AddSingleton(members.Object);
        }
    }

    private static async Task<string> CreateAsync(TicketKit kit, string userId, string accountId)
    {
        var result = await kit.RunAsync(services => services.GetRequiredService<ITicketService>().CreateAsync(new TicketCreateRequest
        {
            Subject = "Can't print",
            Body = "It jams.",
            DeskId = "support",
            Channel = TicketsConstants.Channels.Portal,
            Requester = new PartyRef(PartyKinds.User, userId),
            AccountId = accountId,
        }, TestContext.Current.CancellationToken));

        Assert.True(result.Succeeded, string.Join(" ", result.Errors));

        return result.Ticket.ContentItemId;
    }
}
