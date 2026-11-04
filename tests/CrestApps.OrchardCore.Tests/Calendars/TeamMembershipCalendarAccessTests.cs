using System.Security.Claims;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// A team calendar belongs to a role. The members of the team linked to that role (and of its sub-teams) reach it through
/// the shared team membership, as the users in the role do; a calendar no team links to is left to its role.
/// </summary>
public sealed class TeamMembershipCalendarAccessTests
{
    private static readonly Team _site = new() { ItemId = "site", Name = "Boston", LinkedRoleName = "Support" };
    private static readonly Team _billing = new() { ItemId = "billing", Name = "Billing", ParentId = "site" };

    [Fact]
    public async Task AMemberOfTheLinkedTeam_OrOfItsSubTeam_WritesToTheTeamCalendar()
    {
        // Arrange
        var contributor = CreateContributor(new Dictionary<string, string[]> { ["ben"] = ["billing"] });

        // Act
        var level = await contributor.GetAccessLevelAsync(User("ben"), TeamCalendar("Support"), TestContext.Current.CancellationToken);
        var other = await contributor.GetAccessLevelAsync(User("zoe"), TeamCalendar("Support"), TestContext.Current.CancellationToken);
        var unlinked = await contributor.GetAccessLevelAsync(User("ben"), TeamCalendar("Sales"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CalendarAccessLevel.Write, level);
        Assert.Equal(CalendarAccessLevel.None, other);
        Assert.Equal(CalendarAccessLevel.None, unlinked);
    }

    [Fact]
    public async Task AMembersCalendars_AreTheTeamCalendarsOfTheirTeamsAndTheTeamsAbove()
    {
        // Arrange
        var store = new Mock<ICalendarStore>();
        store.Setup(value => value.ListAsync(It.IsAny<CalendarListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((CalendarListQuery query, CancellationToken _) => query.Types.Contains(CalendarsConstants.Types.Team) && query.OwnerIds.Contains("Support")
                ? [TeamCalendar("Support")]
                : []);
        var contributor = CreateContributor(new Dictionary<string, string[]> { ["ben"] = ["billing"] }, store.Object);

        // Act
        var ids = await contributor.GetCalendarIdsAsync(User("ben"), TestContext.Current.CancellationToken);
        var none = await contributor.GetCalendarIdsAsync(User("zoe"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["calendar-Support"], ids);
        Assert.Empty(none);
    }

    private static TeamMembershipCalendarAccessContributor CreateContributor(Dictionary<string, string[]> teamsOfUser, ICalendarStore store = null)
    {
        var teams = new Mock<ITeamManager>();
        teams.Setup(value => value.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync([_site, _billing]);
        teams.Setup(value => value.GetAncestorsAsync("billing", It.IsAny<CancellationToken>())).ReturnsAsync([_site]);
        teams.Setup(value => value.GetAncestorsAsync("site", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var memberships = new Mock<ITeamMembershipService>();
        memberships.Setup(value => value.GetMembershipsAsync(It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string userId, DateOnly? _, CancellationToken _) => teamsOfUser.TryGetValue(userId, out var ids)
                ? [.. ids.Select(id => new TeamMember { TeamId = id, UserId = userId })]
                : []);
        memberships.Setup(value => value.IsMemberAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string teamId, string userId, DateOnly? _, CancellationToken _) => teamsOfUser.TryGetValue(userId, out var ids)
                && (ids.Contains(teamId) || (teamId == "site" && ids.Contains("billing"))));

        var clock = new Mock<IClock>();
        clock.Setup(value => value.UtcNow).Returns(new DateTime(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc));

        return new TeamMembershipCalendarAccessContributor(teams.Object, memberships.Object, store ?? Mock.Of<ICalendarStore>(), clock.Object);
    }

    private static Calendar TeamCalendar(string role)
        => new() { ItemId = $"calendar-{role}", Type = CalendarsConstants.Types.Team, OwnerKind = CalendarOwnerKind.Role, OwnerId = role };

    private static ClaimsPrincipal User(string userId)
        => new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "Test"));
}
