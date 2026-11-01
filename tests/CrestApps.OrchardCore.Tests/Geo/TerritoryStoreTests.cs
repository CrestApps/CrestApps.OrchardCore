using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// Territories are checked when saved, and the resolver finds them in the store.
/// </summary>
public sealed class TerritoryStoreTests
{
    private const string Square = """{ "type": "Polygon", "coordinates": [ [ [-71.1, 42.3], [-71.0, 42.3], [-71.0, 42.4], [-71.1, 42.4], [-71.1, 42.3] ] ] }""";

    [Fact]
    public async Task ATerritory_IsCheckedAndFound()
    {
        await using var harness = await CreateAsync();

        var refused = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ServiceTerritoryManager>();
            var empty = await manager.NewAsync();

            empty.Name = "Empty";

            var broken = await manager.NewAsync();

            broken.Name = "Broken";
            broken.Shape = "{ \"type\": \"Polygon\" }";

            return (await manager.ValidateAsync(empty)).Errors.Select(error => error.ErrorMessage)
                .Concat((await manager.ValidateAsync(broken)).Errors.Select(error => error.ErrorMessage))
                .ToList();
        }, commit: false);

        Assert.Equal("Draw the territory on the map, list its postal codes, or both.", refused[0]);
        Assert.StartsWith("The shape can't be read:", refused[1]);

        var parent = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ServiceTerritoryManager>();
            var north = await manager.NewAsync();

            north.Name = "North";
            north.Shape = Square;
            north.PostalCodes = ["021 08", "022*"];
            Assert.True((await manager.ValidateAsync(north)).Succeeded);
            await manager.CreateAsync(north);

            return north;
        });

        Assert.Equal(["02108", "022*"], parent.PostalCodes);

        var selfParent = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ServiceTerritoryManager>();
            var north = await manager.FindByIdAsync(parent.ItemId);

            north.ParentId = north.ItemId;

            return (await manager.ValidateAsync(north)).Errors.Select(error => error.ErrorMessage).ToList();
        }, commit: false);

        Assert.Equal(["A territory can't be inside itself."], selfParent);

        var found = await harness.RunAsync(async services =>
        {
            var resolver = services.GetRequiredService<ITerritoryResolver>();

            return (await resolver.ResolveAsync(new GeoPoint(42.35, -71.05), null), await resolver.ResolveAsync(null, "02215"), await resolver.ResolveAsync(null, "10001"));
        }, commit: false);

        Assert.Equal(TerritoryMatchKind.Shape, found.Item1.MatchedBy);
        Assert.Equal(parent.ItemId, found.Item2.Territory.ItemId);
        Assert.Null(found.Item3);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(new Mock<IGeoSettingsService>().Object);
                services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
                services.AddGeoCore();
            },
            GeoIndexSchema.CreateAsync,
            [new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider()],
            [GeoConstants.CollectionName]);
    }
}
