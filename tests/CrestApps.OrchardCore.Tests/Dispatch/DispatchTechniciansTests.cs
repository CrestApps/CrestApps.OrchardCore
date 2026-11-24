using CrestApps.OrchardCore.Dispatch;
using CrestApps.OrchardCore.Dispatch.Core.Services;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Dispatch;

/// <summary>
/// Dispatch sends technicians only: staff members with the Technician capability. Other staff on the same team, such as
/// an office manager, are never listed on the board, suggested or optimized.
/// </summary>
public sealed class DispatchTechniciansTests
{
    [Fact]
    public async Task OnlyStaffWithTheTechnicianCapability_AreDispatched()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var office = await PlanningKit.AddMemberAsync(kit.Harness, "Olga Office", kit.Team);

        // Act
        var workers = await kit.Harness.RunAsync(services => services.GetRequiredService<DispatchResources>().ListWorkersAsync(), commit: false);

        // Assert
        Assert.Contains(DispatchConstants.TechnicianCapability, ana.Capabilities);
        Assert.DoesNotContain(DispatchConstants.TechnicianCapability, office.Capabilities);
        Assert.Equal([ana.ItemId], workers.Select(worker => worker.ItemId));
    }

    [Fact]
    public async Task TheSettingsChooseWhoIsDispatched_AndTheBoardFilterCanShowOthers()
    {
        // Arrange
        await using var kit = await DispatchKit.CreateAsync();
        var ana = await kit.AddTechnicianAsync("Ana Field");
        var ivan = await PlanningKit.AddMemberAsync(kit.Harness, "Ivan Check", kit.Team, staff => staff.Capabilities.Add("Inspector"));

        kit.Settings.TechnicianCapabilities = [DispatchConstants.TechnicianCapability, "Inspector"];

        // Act
        var (both, inspectorsOnly) = await kit.Harness.RunAsync(async services =>
        {
            var resources = services.GetRequiredService<DispatchResources>();

            return (await resources.ListWorkersAsync(), await resources.ListWorkersAsync(["Inspector"]));
        }, commit: false);

        // Assert
        Assert.Equal(new[] { ana.ItemId, ivan.ItemId }.Order(StringComparer.Ordinal), both.Select(worker => worker.ItemId).Order(StringComparer.Ordinal));
        Assert.Equal([ivan.ItemId], inspectorsOnly.Select(worker => worker.ItemId));
    }
}
