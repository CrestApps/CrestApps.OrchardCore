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
}
