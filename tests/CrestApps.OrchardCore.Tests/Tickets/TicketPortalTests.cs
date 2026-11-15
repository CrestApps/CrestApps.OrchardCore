using CrestApps.OrchardCore.Accounts.Models;
using CrestApps.OrchardCore.Accounts.Services;
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
                SeesOwn = TicketPortalAccess.CanSee(ana, await services.GetRequiredService<ITicketService>().GetAsync(anas)),
                SeesBens = TicketPortalAccess.CanSee(ana, await services.GetRequiredService<ITicketService>().GetAsync(bens)),
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
                AnaSeesBens = TicketPortalAccess.CanSee(ana, await services.GetRequiredService<ITicketService>().GetAsync(bens)),
            };
        });

        // Assert
        Assert.Equal("acme", result.Ana.AdminAccountId);
        Assert.Equal(2, result.Organization);
        Assert.True(result.AnaSeesBens);
        Assert.Null(result.Ben.AdminAccountId);
    }

    [Fact]
    public async Task AGuestsLink_OpensOnlyItsOwnRequest()
    {
        // Arrange
        await using var kit = await CreateAsync(adminOf: null);

        // Act
        var result = await kit.ReadAsync(services =>
        {
            var access = services.GetRequiredService<TicketPortalAccess>();
            var token = access.CreateGuestToken("t1");

            return Task.FromResult(new
            {
                Own = access.IsGuestTokenFor(token, "t1"),
                Other = access.IsGuestTokenFor(token, "t2"),
                Forged = access.IsGuestTokenFor("not-a-token", "t1"),
            });
        });

        // Assert
        Assert.True(result.Own);
        Assert.False(result.Other);
        Assert.False(result.Forged);
    }

    private static Task<TicketKit> CreateAsync(string adminOf)
        => TicketKit.CreateAsync(configureServices: services =>
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
        });

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
