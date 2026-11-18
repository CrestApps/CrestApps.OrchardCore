using System.Security.Claims;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.ContentManagement;
using OrchardCore.Security;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Who may do what with tickets: permissions are per desk, viewing has a scope (every ticket, the tickets of the user's
/// teams, or those assigned to them), other operations apply only to tickets the user may view, and a desk without any
/// permission is invisible.
/// </summary>
public sealed class TicketAccessTests
{
    private static readonly TicketDesk _support = new() { ItemId = "support", Name = "Support" };
    private static readonly TicketDesk _hr = new() { ItemId = "hr", Name = "HR" };

    [Fact]
    public async Task ViewScopes_ComeFromThePerDeskPermissions()
    {
        // Arrange
        var access = Create();
        var agent = User("agent-1", TicketsPermissions.ForDesk(TicketOperation.ViewTeam, _support));

        // Act
        var scopes = await access.GetViewScopesAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TicketViewScope.Teams, Assert.Single(scopes).Value);
        Assert.Equal("support", scopes.Keys.Single());
    }

    [Fact]
    public async Task ATeamScope_SeesItsTeamsTickets_AndItsOwn_ButNotOthers()
    {
        // Arrange
        var access = Create();
        var agent = User("agent-1", TicketsPermissions.ForDesk(TicketOperation.ViewTeam, _support), TicketsPermissions.ForDesk(TicketOperation.Reply, _support));

        // Act
        var teamTicket = await access.CanAsync(agent, Ticket("support", team: "tier-1"), TicketOperation.Reply, TestContext.Current.CancellationToken);
        var ownTicket = await access.CanAsync(agent, Ticket("support", team: "tier-2", assignee: "agent-1"), TicketOperation.ViewTeam, TestContext.Current.CancellationToken);
        var otherTicket = await access.CanAsync(agent, Ticket("support", team: "tier-2"), TicketOperation.ViewTeam, TestContext.Current.CancellationToken);
        var hrTicket = await access.CanAsync(agent, Ticket("hr", team: "tier-1"), TicketOperation.ViewTeam, TestContext.Current.CancellationToken);
        var cannotMerge = await access.CanAsync(agent, Ticket("support", team: "tier-1"), TicketOperation.Merge, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(teamTicket);
        Assert.True(ownTicket);
        Assert.False(otherTicket);
        Assert.False(hrTicket);
        Assert.False(cannotMerge);
    }

    [Fact]
    public async Task AScopedQuery_ListsTheDesksByScope_AndAUserWhoMayViewNoneGetsNothing()
    {
        // Arrange
        var access = Create();
        var lead = User("lead-1", TicketsPermissions.ForDesk(TicketOperation.ViewAll, _support), TicketsPermissions.ForDesk(TicketOperation.ViewAssigned, _hr));

        // Act
        var scoped = await access.ApplyScopeAsync(lead, new TicketQuery(), TestContext.Current.CancellationToken);
        var none = await access.ApplyScopeAsync(User("visitor"), new TicketQuery(), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["support"], scoped.AllDeskIds);
        Assert.Equal(["hr"], scoped.AssignedDeskIds);
        Assert.Empty(scoped.TeamDeskIds);
        Assert.Equal("lead-1", scoped.UserId);
        Assert.Null(none);
    }

    private static DefaultTicketAccessService Create()
    {
        var desks = new Mock<ICatalog<TicketDesk>>();

        desks.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_support, _hr]);
        desks.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string id, CancellationToken _) => id == "support" ? _support : id == "hr" ? _hr : null);

        var memberships = new Mock<ITeamMembershipService>();

        memberships.Setup(value => value.GetMembershipsAsync("agent-1", It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TeamMember { TeamId = "tier-1", UserId = "agent-1" }]);

        var services = new ServiceCollection().AddSingleton(memberships.Object).BuildServiceProvider();

        return new DefaultTicketAccessService(new PermissionClaimsAuthorizationService(), desks.Object, services);
    }

    private static ContentItem Ticket(string desk, string team = null, string assignee = null)
    {
        var item = new ContentItem { ContentItemId = "t1", ContentType = "Ticket" };

        item.Apply(new TicketPart { DeskId = desk, TeamId = team, AssigneeId = assignee });

        return item;
    }

    private static ClaimsPrincipal User(string userId, params Permission[] permissions)
        => new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId), .. permissions.Select(permission => new Claim(Permission.ClaimType, permission.Name))],
            "test"));

    // Grants a permission held as a claim, or implied by one held, as Orchard Core's role handler does.
    private sealed class PermissionClaimsAuthorizationService : IAuthorizationService
    {
        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, IEnumerable<IAuthorizationRequirement> requirements)
            => Task.FromResult(requirements.OfType<PermissionRequirement>().All(requirement => Holds(user, requirement.Permission))
                ? AuthorizationResult.Success()
                : AuthorizationResult.Failed());

        public Task<AuthorizationResult> AuthorizeAsync(ClaimsPrincipal user, object resource, string policyName)
            => Task.FromResult(AuthorizationResult.Failed());

        private static bool Holds(ClaimsPrincipal user, Permission permission)
            => user.HasClaim(Permission.ClaimType, permission.Name) || (permission.ImpliedBy?.Any(implied => Holds(user, implied)) ?? false);
    }
}
