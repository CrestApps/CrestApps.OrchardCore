using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// A worker resolves to the same time zone in Workforce and in Dispatch: their own, their team's, or the nearest parent
/// team's.
/// </summary>
public sealed class WorkerTimeZoneResolutionTests
{
    [Fact]
    public async Task AWorkerOnASubTeamWithNoTimeZone_TakesTheParentTeamsZone_InWorkforceAndInDispatch()
    {
        // Arrange: the crew has no zone of its own; the region above it is in Chicago.
        await using var kit = await DispatchKit.CreateAsync();
        var region = await WorkforceTests.AddTeamAsync(kit.Harness, "Central", TeamKind.Region);

        await PlanningKit.UpdateTeamAsync(kit.Harness, region, team => team.TimeZoneId = "America/Chicago");

        var crew = await WorkforceTests.AddTeamAsync(kit.Harness, "Crew", TeamKind.Team, region.ItemId);

        await PlanningKit.UpdateTeamAsync(kit.Harness, crew, team => team.TimeZoneId = null);

        var worker = await WorkforceTests.AddWorkerAsync(kit.Harness, "Ana", profile =>
        {
            profile.PrimaryTeamId = crew.ItemId;
            profile.TeamMemberships.Add(new TeamMembership { TeamId = crew.ItemId });
        });

        // Act
        var (workforceZone, teamZone, dispatchZone) = await kit.Harness.RunAsync(async services =>
        {
            var timeZones = services.GetRequiredService<IWorkerTimeZoneService>();
            var availability = await services.GetRequiredService<DispatchResources>().GetAvailabilityAsync(worker, DispatchKit.Monday(8), DispatchKit.Monday(16));

            return (await timeZones.GetAsync(worker, null), await timeZones.GetTeamAsync(crew.ItemId), availability.TimeZoneId);
        }, commit: false);

        // Assert
        Assert.Equal("America/Chicago", workforceZone);
        Assert.Equal("America/Chicago", teamZone);
        Assert.Equal(workforceZone, dispatchZone);
    }

    [Fact]
    public async Task AWorkersOwnTimeZone_WinsOverTheirTeams_InWorkforceAndInDispatch()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var crew = await WorkforceTests.AddTeamAsync(kit.Harness, "Crew", TeamKind.Team);
        var worker = await WorkforceTests.AddWorkerAsync(kit.Harness, "Ana", profile =>
        {
            profile.TimeZoneId = "Europe/Paris";
            profile.TeamMemberships.Add(new TeamMembership { TeamId = crew.ItemId });
        });

        // Act
        var (workforceZone, dispatchZone) = await kit.Harness.RunAsync(async services => (
            await services.GetRequiredService<IWorkerTimeZoneService>().GetAsync(worker, null),
            (await services.GetRequiredService<DispatchResources>().GetAvailabilityAsync(worker, DispatchKit.Monday(8), DispatchKit.Monday(16))).TimeZoneId), commit: false);

        // Assert
        Assert.Equal("Europe/Paris", workforceZone);
        Assert.Equal(workforceZone, dispatchZone);
    }
}
