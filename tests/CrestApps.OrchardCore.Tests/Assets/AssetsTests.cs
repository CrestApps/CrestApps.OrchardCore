using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Indexes;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Assets;

/// <summary>
/// Assets get numbers, can't sit inside themselves, need an owner when a customer owns them, and keep append-only meter
/// readings that a cumulative meter can't go back on.
/// </summary>
public sealed class AssetsTests
{
    [Fact]
    public async Task Assets_AreNumbered_AndCheckedWhenSaved()
    {
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness);

        var (boiler, pump) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var first = await manager.NewAsync();

            first.Name = "Boiler";
            first.AssetTypeId = type.ItemId;
            await manager.CreateAsync(first);

            var second = await manager.NewAsync();

            second.Name = "Circulating pump";
            second.AssetTypeId = type.ItemId;
            second.ParentAssetId = first.ItemId;
            await manager.CreateAsync(second);

            return (first, second);
        });

        Assert.Equal("A-00001", boiler.Number);
        Assert.Equal("A-00002", pump.Number);

        var errors = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var stored = await manager.FindByIdAsync(boiler.ItemId);

            // The boiler would sit inside its own pump.
            stored.ParentAssetId = pump.ItemId;
            stored.Number = "A-00002";
            stored.OwnerKind = AssetOwnerKind.Customer;

            return (await manager.ValidateAsync(stored)).Errors.Select(error => error.ErrorMessage).ToList();
        }, commit: false);

        Assert.Equal(
            ["Say which customer or company owns the asset.", "Another asset has the number A-00002.", "An asset can't be inside itself."],
            errors);

        var tree = await harness.RunAsync(async services =>
        {
            var service = services.GetRequiredService<AssetService>();
            var stored = await services.GetRequiredService<AssetStore>().FindByIdAsync(pump.ItemId);

            return (await service.ChildrenAsync(boiler.ItemId), await service.AncestorsAsync(stored));
        }, commit: false);

        Assert.Equal(["Circulating pump"], tree.Item1.Select(asset => asset.Name));
        Assert.Equal(["Boiler"], tree.Item2.Select(asset => asset.Name));
    }

    [Fact]
    public async Task ACumulativeMeter_OnlyGoesUp()
    {
        await using var harness = await CreateAsync();
        var type = await AddTypeAsync(harness);
        var asset = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var item = await manager.NewAsync();

            item.Name = "Forklift";
            item.AssetTypeId = type.ItemId;
            await manager.CreateAsync(item);

            return item;
        });

        async Task<IReadOnlyList<string>> RecordAsync(string meter, decimal value, int hour)
            => await harness.RunAsync(services => services.GetRequiredService<AssetService>().RecordReadingAsync(new AssetMeterReading
            {
                AssetId = asset.ItemId,
                Meter = meter,
                Value = value,
                Utc = CalendarTestHarness.Now.AddHours(hour),
                Source = MeterReadingSource.Manual,
            }));

        Assert.Empty(await RecordAsync("hours run", 120, 0));
        Assert.Equal(["Hours run only goes up: the last reading was 120."], await RecordAsync("Hours run", 110, 1));
        Assert.Empty(await RecordAsync("Hours run", 135, 2));
        Assert.Equal(["Forklift has no meter called Miles."], await RecordAsync("Miles", 3, 3));
        Assert.Empty(await RecordAsync("Pressure", 1.2m, 4));
        Assert.Empty(await RecordAsync("Pressure", 0.9m, 5));

        var readings = await harness.RunAsync(services => services.GetRequiredService<AssetService>().ReadingsAsync(asset.ItemId), commit: false);

        Assert.Equal([0.9m, 1.2m, 135m, 120m], readings.Select(reading => reading.Value));
        Assert.Equal("Hours run", readings[2].Meter);
    }

    internal static Task<AssetType> AddTypeAsync(CalendarTestHarness harness, AssetCategory category = AssetCategory.Equipment)
        => harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetTypeManager>();
            var type = await manager.NewAsync();

            type.Name = category.ToString();
            type.Category = category;
            type.Meters = [new MeterDefinition { Name = "Hours run", Unit = "h" }, new MeterDefinition { Name = "Pressure", Unit = "bar", Cumulative = false }];
            await manager.CreateAsync(type);

            return type;
        });

    internal static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
        => CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddAssetsCore();
                configure?.Invoke(services);
            },
            AssetsIndexSchema.CreateAsync,
            [new AssetTypeIndexProvider(), new AssetIndexProvider(), new AssetMeterReadingIndexProvider()],
            [AssetsConstants.CollectionName]);
}
