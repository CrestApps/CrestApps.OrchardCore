using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tickets;
using CrestApps.OrchardCore.Tickets.Core.Services;
using CrestApps.OrchardCore.Tickets.Models;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Tests.Tickets;

/// <summary>
/// Desk assignment on a real store with the real team assignment service and ticket load: when no one on the team is
/// available the ticket stays unassigned and its timeline says why; a source that is off by default (Contact Center
/// presence) counts only on a desk that turns it on; and a desk that honors no source assigns whoever is in the team.
/// </summary>
public sealed class TicketAssignmentSqliteTests
{
    [Fact]
    public async Task WhenNoOneIsAvailable_TheTicketStaysUnassigned_AndItsTimelineSaysWhy()
    {
        // Arrange
        await using var kit = await CreateAsync(new Source("Shifts", honoredByDefault: true));

        // Act
        var created = await kit.CreateAsync();

        // Assert
        var part = (await kit.GetAsync(created.Ticket.ContentItemId)).GetOrCreate<TicketPart>();
        Assert.Null(part.AssigneeId);
        Assert.Contains(await kit.EntriesAsync(created.Ticket.ContentItemId), entry => entry.Body?.StartsWith("Not assigned: no one on the team is available now", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task ASourceOffByDefault_CountsOnlyOnADeskThatTurnsItOn()
    {
        // Arrange
        await using var byDefault = await CreateAsync(new Source("ContactCenterPresence", honoredByDefault: false));
        await using var turnedOn = await CreateAsync(new Source("ContactCenterPresence", honoredByDefault: false), desk => desk.AvailabilitySources = ["ContactCenterPresence"]);

        // Act
        var assigned = await byDefault.CreateAsync();
        var unassigned = await turnedOn.CreateAsync();

        // Assert
        Assert.Equal("ana", (await byDefault.GetAsync(assigned.Ticket.ContentItemId)).GetOrCreate<TicketPart>().AssigneeId);
        Assert.Null((await turnedOn.GetAsync(unassigned.Ticket.ContentItemId)).GetOrCreate<TicketPart>().AssigneeId);
    }

    [Fact]
    public async Task ADeskThatHonorsNoSource_AssignsWhoeverIsInTheTeam()
    {
        // Arrange
        await using var kit = await CreateAsync(new Source("Shifts", honoredByDefault: true), desk => desk.AvailabilitySources = []);

        // Act
        var created = await kit.CreateAsync();

        // Assert
        Assert.Equal("ana", (await kit.GetAsync(created.Ticket.ContentItemId)).GetOrCreate<TicketPart>().AssigneeId);
    }

    private static Task<TicketKit> CreateAsync(Source everyoneUnavailable, Action<TicketDesk> configureDesk = null)
        => TicketKit.CreateAsync(
            configureDesk: desk =>
            {
                desk.DefaultTeamId = "t1";
                desk.AssignmentStrategy = TeamAssignmentStrategies.LeastLoaded;
                configureDesk?.Invoke(desk);
            },
            configureServices: services =>
            {
                services.AddScoped<ITeamAssignmentService, DefaultTeamAssignmentService>();
                services.AddSingleton<ITeamMembershipService>(new Membership());
                services.AddTeamMemberLoadSource<TicketTeamMemberLoadSource>();
                services.AddSingleton<ITeamMemberAvailabilitySource>(everyoneUnavailable);
            });

    private sealed class Source : ITeamMemberAvailabilitySource
    {
        public Source(string name, bool honoredByDefault)
        {
            Name = name;
            HonoredByDefault = honoredByDefault;
        }

        public string Name { get; }

        public bool HonoredByDefault { get; }

        public Task<IReadOnlyCollection<string>> GetUnavailableAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult(userIds);
    }

    private sealed class Membership : ITeamMembershipService
    {
        private static readonly IReadOnlyList<TeamMember> _members = [new TeamMember { TeamId = "t1", UserId = "ana" }];

        public Task<IReadOnlyList<TeamMember>> GetMembersAsync(string teamId, bool includeSubTeams = true, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult(teamId == "t1" ? _members : []);

        public Task<IReadOnlyList<TeamMember>> GetMembershipsAsync(string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TeamMember>>([.. _members.Where(member => member.UserId == userId)]);

        public Task<bool> IsMemberAsync(string teamId, string userId, DateOnly? on = null, CancellationToken cancellationToken = default)
            => Task.FromResult(_members.Any(member => member.UserId == userId));
    }
}
