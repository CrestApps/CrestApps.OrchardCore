using CrestApps.OrchardCore.Assets;
using CrestApps.OrchardCore.Assets.Core.Services;
using CrestApps.OrchardCore.Assets.Fleet;
using CrestApps.OrchardCore.Assets.Models;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Sql;
using Asset = CrestApps.OrchardCore.Assets.Models.Asset;

namespace CrestApps.OrchardCore.Tests.Assets;

/// <summary>
/// A schedulable vehicle gets an Equipment calendar that refuses double-booking, and a van that carries stock gets its
/// own Inventory location owned by it.
/// </summary>
public sealed class FleetTests
{
    [Fact]
    public async Task ASchedulableVehicle_CantBeDoubleBooked()
    {
        await using var harness = await CreateAsync();
        var van = await AddVanAsync(harness, schedulable: true, carriesStock: false);

        Assert.False(string.IsNullOrEmpty(van.Fleet.CalendarId));

        var start = CalendarTestHarness.Now.Date.AddDays(1).AddHours(9);
        var first = await harness.CreateEventAsync(van.Fleet.CalendarId, start, start.AddHours(2));
        var overlapping = await harness.CreateEventAsync(van.Fleet.CalendarId, start.AddHours(1), start.AddHours(3));
        var after = await harness.CreateEventAsync(van.Fleet.CalendarId, start.AddHours(2), start.AddHours(4));

        Assert.True(first.Succeeded);
        Assert.False(overlapping.Succeeded);
        Assert.True(after.Succeeded);

        // Renaming the van renames its calendar; saving again keeps the same one.
        var calendarId = van.Fleet.CalendarId;
        var renamed = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<AssetManager>();
            var stored = await manager.FindByIdAsync(van.ItemId);

            stored.Name = "Van 12 (blue)";
            await manager.UpdateAsync(stored);

            return (stored.Fleet.CalendarId, (await services.GetRequiredService<CrestApps.OrchardCore.Calendars.Services.ICalendarService>().FindByIdAsync(stored.Fleet.CalendarId)).Name);
        });

        Assert.Equal((calendarId, "Van 12 (blue)"), renamed);
    }

    [Fact]
    public async Task AVanThatCarriesStock_GetsItsOwnLocation()
    {
        await using var harness = await CreateAsync();
        var van = await AddVanAsync(harness, schedulable: false, carriesStock: true);

        var location = await harness.RunAsync(services => services.GetRequiredService<StockLocationStore>().FindByOwnerAsync(AssetsConstants.Source, van.ItemId), commit: false);

        Assert.Equal(van.Fleet.StockLocationId, location.ItemId);
        Assert.Equal("Van 12", location.Name);
        Assert.Null(van.Fleet.CalendarId);
    }

    private static Task<Asset> AddVanAsync(CalendarTestHarness harness, bool schedulable, bool carriesStock)
        => harness.RunAsync(async services =>
        {
            var type = await AssetsTests.AddTypeAsync(harness, AssetCategory.Vehicle);
            var manager = services.GetRequiredService<AssetManager>();
            var van = await manager.NewAsync();

            van.Name = "Van 12";
            van.AssetTypeId = type.ItemId;
            van.Fleet = new AssetFleet { Schedulable = schedulable, CarriesStock = carriesStock, Plate = "8ABC123" };
            await manager.CreateAsync(van);

            return van;
        });

    private static async Task<CalendarTestHarness> CreateAsync()
    {
        var harness = await AssetsTests.CreateAsync(services =>
        {
            services.AddCalendarType(AssetsConstants.EquipmentCalendarType, type => type.WithOwner(CrestApps.OrchardCore.Calendars.Models.CalendarOwnerKind.Resource).LimitConcurrent(1));
            services.AddScoped<StockLocationStore>();
            services.AddScoped<ICatalogEntryHandler<Asset>, FleetCalendarHandler>();
            services.AddScoped<ICatalogEntryHandler<Asset>, FleetStockLocationHandler>();
        });

        harness.Store.RegisterIndexes([new StockLocationIndexProvider()], CrestApps.OrchardCore.Inventory.Core.InventoryConstants.CollectionName);
        await harness.Store.InitializeCollectionAsync(CrestApps.OrchardCore.Inventory.Core.InventoryConstants.CollectionName, TestContext.Current.CancellationToken);

        await using (var session = harness.Store.CreateSession())
        {
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var builder = new global::YesSql.Sql.SchemaBuilder(harness.Store.Configuration, transaction);

            await builder.CreateMapIndexTableAsync<StockLocationIndex>(table => table
                .Column<string>("ItemId", column => column.WithLength(26))
                .Column<string>("Name", column => column.Nullable().WithLength(255))
                .Column<string>("OwnerKind", column => column.Nullable().WithLength(64))
                .Column<string>("OwnerId", column => column.Nullable().WithLength(26)),
                collection: CrestApps.OrchardCore.Inventory.Core.InventoryConstants.CollectionName);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
        }

        return harness;
    }
}
