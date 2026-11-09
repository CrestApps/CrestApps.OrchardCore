using CrestApps.Core;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.WorkOrders;
using CrestApps.OrchardCore.WorkOrders.Core;
using CrestApps.OrchardCore.WorkOrders.Core.Indexes;
using CrestApps.OrchardCore.WorkOrders.Core.Services;
using CrestApps.OrchardCore.WorkOrders.Models;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Assets;

/// <summary>
/// The asset type and work order type lists count their records and page their types in the database instead of loading
/// every row.
/// </summary>
public sealed class AssetTypeListingTests
{
    [Fact]
    public async Task CountByTypeAsync_CountsTheAssetsOfEachType()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync();
        var pumps = await AddTypeAsync(harness, "Pumps");
        var boilers = await AddTypeAsync(harness, "Boilers");
        var empty = await AddTypeAsync(harness, "Empty");

        await AddAssetsAsync(harness, pumps, 3);
        await AddAssetsAsync(harness, boilers, 1);

        // Act
        var counts = await harness.RunAsync(services => services.GetRequiredService<AssetStore>().CountByTypeAsync([pumps.ItemId, boilers.ItemId, empty.ItemId]), commit: false);

        // Assert
        Assert.Equal(3, counts[pumps.ItemId]);
        Assert.Equal(1, counts[boilers.ItemId]);
        Assert.Equal(0, counts[empty.ItemId]);
    }

    [Fact]
    public async Task PageAsync_ReturnsOnePageOfTypesByName_AndHowManyThereAre()
    {
        // Arrange
        await using var harness = await AssetsTests.CreateAsync();

        foreach (var name in new[] { "Delta", "Alpha", "Echo", "Charlie", "Bravo" })
        {
            await AddTypeAsync(harness, name);
        }

        // Act
        var (count, items) = await harness.RunAsync(services => services.GetRequiredService<AssetTypeStore>().PageAsync(2, 2), commit: false);

        // Assert
        Assert.Equal(5, count);
        Assert.Equal(["Charlie", "Delta"], items.Select(item => item.Name));
    }

    [Fact]
    public async Task WorkOrderStore_CountByTypeAsync_CountsTheWorkOrdersOfEachType()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync(
            services => services.AddWorkOrdersCore(),
            WorkOrdersIndexSchema.CreateAsync,
            [new WorkOrderIndexProvider()],
            [WorkOrdersConstants.CollectionName]);

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<WorkOrderStore>();

            foreach (var (typeId, number) in new[] { ("repair", 1), ("repair", 2), ("install", 3) })
            {
                await store.CreateAsync(new WorkOrder { ItemId = UniqueId.GenerateId(), TypeId = typeId, Number = "WO-" + number, Summary = "Job " + number });
            }
        });

        // Act
        var counts = await harness.RunAsync(services => services.GetRequiredService<WorkOrderStore>().CountByTypeAsync(["repair", "install", "survey"]), commit: false);

        // Assert
        Assert.Equal((2, 1, 0), (counts["repair"], counts["install"], counts["survey"]));
    }

    private static Task<AssetType> AddTypeAsync(CalendarTestHarness harness, string name)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetTypeManager>();
            var type = await manager.NewAsync();

            type.Name = name;
            await manager.CreateAsync(type);

            return type;
        });

    private static Task AddAssetsAsync(CalendarTestHarness harness, AssetType type, int count)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();

            for (var index = 0; index < count; index++)
            {
                var asset = await manager.NewAsync();

                asset.Name = $"{type.Name} {index + 1}";
                asset.AssetTypeId = type.ItemId;
                await manager.CreateAsync(asset);
            }
        });
}
