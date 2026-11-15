using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;

namespace CrestApps.OrchardCore.Tests.Teams;

/// <summary>
/// Team assignment picks the member who should take a piece of work: in turn or by least open work, among the members
/// with a login who are available and pass the caller's filter. It keeps no state, so every node picks the same person.
/// </summary>
public sealed class TeamAssignmentServiceTests
{
    private const string WorkKind = "Ticket";

    private static readonly DateTime _noon = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task LeastLoaded_PicksTheMemberWithTheLeastOpenWork()
    {
        // Arrange
        var service = Create(
            members: ["ana", "ben", "cy"],
            load: new() { ["ana"] = Load(3), ["ben"] = Load(1), ["cy"] = Load(2) });

        // Act
        var picked = await service.SelectAsync(Request(TeamAssignmentStrategies.LeastLoaded), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ben", picked.UserId);
    }

    [Fact]
    public async Task RoundRobin_PicksTheMemberGivenWorkLongestAgo_AndNewMembersFirst()
    {
        // Arrange
        var load = new Dictionary<string, TeamMemberLoad>
        {
            ["ana"] = Load(0, _noon.AddHours(-1)),
            ["ben"] = Load(9, _noon.AddHours(-3)),
            ["cy"] = Load(0, _noon.AddHours(-2)),
        };

        // Act
        var inTurn = await Create(["ana", "ben", "cy"], load).SelectAsync(Request(TeamAssignmentStrategies.RoundRobin), TestContext.Current.CancellationToken);
        var withNewMember = await Create(["ana", "ben", "cy", "dee"], load).SelectAsync(Request(TeamAssignmentStrategies.RoundRobin), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ben", inTurn.UserId);
        Assert.Equal("dee", withNewMember.UserId);
    }

    [Fact]
    public async Task Selection_LeavesOutUnavailableExcludedFilteredAndFullMembers()
    {
        // Arrange
        var service = Create(
            members: ["ana", "ben", "cy", "dee", "eve"],
            load: new() { ["ana"] = Load(0), ["ben"] = Load(0), ["cy"] = Load(0), ["dee"] = Load(5), ["eve"] = Load(2) },
            unavailable: ["ana"]);

        var request = Request(TeamAssignmentStrategies.LeastLoaded);
        request.ExcludedUserIds = ["ben"];
        request.MaxOpen = 5;
        request.CandidateFilter = (candidates, _) => Task.FromResult<IReadOnlyList<TeamMember>>([.. candidates.Where(member => member.UserId != "cy")]);

        // Act
        var picked = await service.SelectAsync(request, TestContext.Current.CancellationToken);

        request.OnlyAvailable = false;
        var ignoringAvailability = await service.SelectAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("eve", picked.UserId);
        Assert.Equal("ana", ignoringAvailability.UserId);
    }

    [Fact]
    public async Task Selection_ReturnsNobody_WhenNoMemberQualifies()
    {
        // Arrange
        var withoutLogins = new DefaultTeamAssignmentService(new Membership([new TeamMember { TeamId = "t1", StaffMemberId = "s1" }]), [], []);
        var allAway = Create(["ana"], [], unavailable: ["ana"]);

        // Act and assert
        Assert.Null(await withoutLogins.SelectAsync(Request(TeamAssignmentStrategies.LeastLoaded), TestContext.Current.CancellationToken));
        Assert.Null(await allAway.SelectAsync(Request(TeamAssignmentStrategies.RoundRobin), TestContext.Current.CancellationToken));
        Assert.Null(await allAway.SelectAsync(new TeamAssignmentRequest(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Selection_CountsAPersonOnce_AndBreaksTiesByUserId()
    {
        // Arrange: "ben" is a member of the team and of a sub-team; nobody has any work.
        var membership = new Membership(
        [
            new TeamMember { TeamId = "t1", UserId = "cy" },
            new TeamMember { TeamId = "t1", UserId = "ben" },
            new TeamMember { TeamId = "t1-a", UserId = "ben" },
        ]);
        var service = new DefaultTeamAssignmentService(membership, [], []);

        // Act
        var picked = await service.SelectAsync(Request(TeamAssignmentStrategies.RoundRobin), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ben", picked.UserId);
    }

    [Fact]
    public async Task SourcesCombine_AMemberAnySourceReportsIsUnavailable_AndAFailingSourceDoesntBlock()
    {
        // Arrange
        var service = new DefaultTeamAssignmentService(
            new Membership([.. new[] { "ana", "ben", "cy" }.Select(userId => new TeamMember { TeamId = "t1", UserId = userId })]),
            [],
            [new AvailabilitySource(["ana"], "Shifts"), new AvailabilitySource(["ben"], "Absences"), new AvailabilitySource(["cy"], "Broken") { Throws = true }]);

        // Act
        var picked = await service.PickAsync(Request(TeamAssignmentStrategies.LeastLoaded), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TeamAssignmentOutcome.Picked, picked.Outcome);
        Assert.Equal("cy", picked.Member.UserId);
    }

    [Fact]
    public async Task WhenEveryoneIsUnavailable_NobodyIsPicked_AndTheOutcomeSaysWhy()
    {
        // Arrange
        var service = Create(["ana", "ben"], [], unavailable: ["ana", "ben"]);

        // Act
        var unavailable = await service.PickAsync(Request(TeamAssignmentStrategies.RoundRobin), TestContext.Current.CancellationToken);
        var unqualified = await Create(["ana"], []).PickAsync(new TeamAssignmentRequest { TeamId = "t1", WorkKind = WorkKind, CandidateFilter = (_, _) => Task.FromResult<IReadOnlyList<TeamMember>>([]) }, TestContext.Current.CancellationToken);
        var full = await Create(["ana"], new() { ["ana"] = Load(5) }).PickAsync(new TeamAssignmentRequest { TeamId = "t1", WorkKind = WorkKind, MaxOpen = 5 }, TestContext.Current.CancellationToken);
        var empty = await Create([], []).PickAsync(Request(TeamAssignmentStrategies.LeastLoaded), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TeamAssignmentOutcome.NoneAvailable, unavailable.Outcome);
        Assert.Null(unavailable.Member);
        Assert.Equal(TeamAssignmentOutcome.NoneQualified, unqualified.Outcome);
        Assert.Equal(TeamAssignmentOutcome.AllAtCapacity, full.Outcome);
        Assert.Equal(TeamAssignmentOutcome.NoMembers, empty.Outcome);
    }

    [Fact]
    public async Task ARequestHonorsOnlyTheSourcesItNames()
    {
        // Arrange
        var service = new DefaultTeamAssignmentService(
            new Membership([new TeamMember { TeamId = "t1", UserId = "ana" }]),
            [],
            [new AvailabilitySource(["ana"], "ContactCenterPresence")]);

        // Act
        var honoringNone = await service.PickAsync(new TeamAssignmentRequest { TeamId = "t1", WorkKind = WorkKind, AvailabilitySources = [] }, TestContext.Current.CancellationToken);
        var honoringPresence = await service.PickAsync(new TeamAssignmentRequest { TeamId = "t1", WorkKind = WorkKind, AvailabilitySources = ["ContactCenterPresence"] }, TestContext.Current.CancellationToken);
        var honoringAll = await service.PickAsync(new TeamAssignmentRequest { TeamId = "t1", WorkKind = WorkKind }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("ana", honoringNone.Member?.UserId);
        Assert.Equal(TeamAssignmentOutcome.NoneAvailable, honoringPresence.Outcome);
        Assert.Equal(TeamAssignmentOutcome.NoneAvailable, honoringAll.Outcome);
    }

    private static TeamAssignmentRequest Request(string strategy)
        => new() { TeamId = "t1", WorkKind = WorkKind, Strategy = strategy };

    private static TeamMemberLoad Load(int open, DateTime? lastAssignedUtc = null)
        => new() { Open = open, LastAssignedUtc = lastAssignedUtc ?? _noon.AddDays(-1) };

    private static DefaultTeamAssignmentService Create(string[] members, Dictionary<string, TeamMemberLoad> load, string[] unavailable = null)
        => new(
            new Membership([.. members.Select(userId => new TeamMember { TeamId = "t1", UserId = userId })]),
            [new LoadSource(load), new LoadSource([]) { WorkKind = "Other" }],
            unavailable is null ? [] : [new AvailabilitySource(unavailable)]);

    private sealed class Membership : ITeamMembershipService
    {
        private readonly IReadOnlyList<TeamMember> _members;

        public Membership(IReadOnlyList<TeamMember> members)
        {
            _members = members;
        }

        public Task<IReadOnlyList<TeamMember>> GetMembersAsync(string teamId, bool includeSubTeams = true, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_members);

        public Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TeamMember>>([.. _members.Where(member => member.UserId == userId)]);

        public Task<bool> IsMemberAsync(string teamId, string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_members.Any(member => member.UserId == userId));
    }

    private sealed class LoadSource : ITeamMemberLoadSource
    {
        private readonly Dictionary<string, TeamMemberLoad> _load;

        public LoadSource(Dictionary<string, TeamMemberLoad> load)
        {
            _load = load;
        }

        public string WorkKind { get; init; } = TeamAssignmentServiceTests.WorkKind;

        public Task<IReadOnlyDictionary<string, TeamMemberLoad>> GetLoadAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, TeamMemberLoad>>(_load.Where(pair => userIds.Contains(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value));
    }

    private sealed class AvailabilitySource : ITeamMemberAvailabilitySource
    {
        private readonly string[] _unavailable;

        public AvailabilitySource(string[] unavailable, string name = "Source")
        {
            _unavailable = unavailable;
            Name = name;
        }

        public string Name { get; }

        public bool Throws { get; init; }

        public Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Throws
                ? throw new InvalidOperationException("The calendar can't be read.")
                : Task.FromResult<IReadOnlyCollection<string>>([.. _unavailable.Where(userIds.Contains)]);
    }
}
