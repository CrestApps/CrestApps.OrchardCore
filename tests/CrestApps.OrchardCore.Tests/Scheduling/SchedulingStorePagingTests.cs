using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The appointment type and resource lists page and count in SQL rather than loading every record, and a page holds the
/// records in name order.
/// </summary>
public sealed class SchedulingStorePagingTests
{
    [Fact]
    public async Task AppointmentTypes_ArePagedAndCountedInNameOrder()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentTypeStore>();

            foreach (var name in new[] { "Delta", "Alpha", "Echo", "Charlie", "Bravo" })
            {
                await store.CreateAsync(new AppointmentType { Name = name, DurationMinutes = 30 });
            }
        });

        // Act
        var (count, page) = await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().PageAsync(2, 2, TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Equal(5, count);
        Assert.Equal(["Charlie", "Delta"], page.Select(type => type.Name));
    }

    [Fact]
    public async Task SchedulingResources_ArePagedAndCountedInNameOrder()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync();

        await SchedulingTestSupport.AddResourceAsync(harness, "Room B");
        await SchedulingTestSupport.AddResourceAsync(harness, "Room A");
        await SchedulingTestSupport.AddResourceAsync(harness, "Room C");

        // Act
        var (count, page) = await harness.RunAsync(services => services.GetRequiredService<SchedulingResourceStore>().PageAsync(0, 2, TestContext.Current.CancellationToken), commit: false);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(["Room A", "Room B"], page.Select(resource => resource.Name));
    }
}
