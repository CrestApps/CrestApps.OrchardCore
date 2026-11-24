using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// Teams answers who is in a team through <see cref="ITeamMembershipService"/>, and Workforce gives its workers as the
/// members, so a module reads team membership without naming Workforce.
/// </summary>
public sealed class WorkforceTeamMembershipTests
{
    private static readonly DateOnly _today = new(2026, 11, 4);

    [Fact]
    public async Task ATeamsMembers_AreItsStaff_WithOrWithoutAUser_AndThoseOfItsSubTeams()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var site = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);
        var billing = await WorkforceTests.AddTeamAsync(harness, "Billing", TeamKind.Team, site.ItemId);

        await WorkforceTests.AddWorkerAsync(harness, "Ana Lopez", worker =>
        {
            worker.UserId = "ana";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = site.ItemId, Role = TeamMemberRole.Manager });
        });
        await WorkforceTests.AddWorkerAsync(harness, "Ben Ito", worker =>
        {
            worker.UserId = "ben";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = billing.ItemId });
        });
        var cara = await WorkforceTests.AddWorkerAsync(harness, "Cara Diaz", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = billing.ItemId }));

        // Act
        var (siteMembers, billingOnly, directOnly) = await harness.RunAsync(async services =>
        {
            var memberships = services.GetRequiredService<ITeamMembershipService>();

            return (
                await memberships.GetMembersAsync(site.ItemId, includeSubTeams: true, _today),
                await memberships.GetMembersAsync(billing.ItemId, includeSubTeams: true, _today),
                await memberships.GetMembersAsync(site.ItemId, includeSubTeams: false, _today));
        }, commit: false);

        // Assert
        // Cara has no account: she is a member through her staff record alone.
        Assert.Equal(["ana", "ben", "staff:" + cara.ItemId], siteMembers.Select(member => member.UserId ?? "staff:" + member.StaffMemberId).Order());
        Assert.Equal(TeamMemberRoles.Manager, siteMembers.Single(member => member.UserId == "ana").Role);
        Assert.Equal(["ben", "staff:" + cara.ItemId], billingOnly.Select(member => member.UserId ?? "staff:" + member.StaffMemberId).Order());
        Assert.Equal(["ana"], directOnly.Select(member => member.UserId));
    }

    [Fact]
    public async Task AUsersTeams_AndMembership_FollowTheMembershipDates()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var site = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);
        var billing = await WorkforceTests.AddTeamAsync(harness, "Billing", TeamKind.Team, site.ItemId);
        var archive = await WorkforceTests.AddTeamAsync(harness, "Archive", TeamKind.Team);

        await WorkforceTests.AddWorkerAsync(harness, "Ben Ito", worker =>
        {
            worker.UserId = "ben";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = billing.ItemId, EffectiveFrom = _today.AddDays(-30) });
            worker.TeamMemberships.Add(new TeamMembership { TeamId = archive.ItemId, EffectiveTo = _today.AddDays(-1) });
        });

        // Act
        var (today, ever, inSite, inArchive) = await harness.RunAsync(async services =>
        {
            var memberships = services.GetRequiredService<ITeamMembershipService>();

            return (
                await memberships.GetMembershipsAsync("ben", _today),
                await memberships.GetMembershipsAsync("ben"),
                await memberships.IsMemberAsync(site.ItemId, "ben", _today),
                await memberships.IsMemberAsync(archive.ItemId, "ben", _today));
        }, commit: false);

        // Assert
        Assert.Equal([billing.ItemId], today.Select(membership => membership.TeamId));
        Assert.Equal(2, ever.Count);
        Assert.Equal(_today.AddDays(-30), ever.Single(membership => membership.TeamId == billing.ItemId).EffectiveFrom);
        Assert.True(inSite);
        Assert.False(inArchive);
    }
}
