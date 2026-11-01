using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Indexes;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using CrestApps.OrchardCore.Tests.Workforce;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Geo;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// Team locations and home bases with an address and no coordinates are geocoded in the background.
/// </summary>
public sealed class WorkforceGeocodingTests
{
    [Fact]
    public async Task ATeamsAddress_IsGeocodedInTheBackground()
    {
        var geocoder = new Mock<IGeocodingProvider>();

        geocoder.SetupGet(g => g.Name).Returns("Fake");
        geocoder.Setup(g => g.GeocodeAsync(It.Is<GeocodeQuery>(q => q.Text == "1 City Hall Square, Boston"), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GeocodeCandidate { Point = new GeoPoint(42.3603, -71.0580), Confidence = 1 }]);

        await using var harness = await WorkforceTests.CreateAsync(
            services =>
            {
                var settings = new Mock<IGeoSettingsService>();

                settings.Setup(s => s.GetAsync()).ReturnsAsync(new GeoSettings { GeocodingProvider = "Fake" });
                services.AddSingleton(settings.Object);
                services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
                services.AddSingleton(geocoder.Object);
                services.AddGeoCore();
                services.AddWorkforceGeocoding();
            },
            GeoIndexSchema.CreateAsync,
            [new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider()]);

        await harness.Store.InitializeCollectionAsync(GeoConstants.CollectionName, TestContext.Current.CancellationToken);

        var team = await WorkforceTests.AddTeamAsync(harness, "Boston", TeamKind.Site);

        await harness.RunAsync(async services =>
        {
            var teams = services.GetRequiredService<ITeamManager>();
            var stored = await teams.FindByIdAsync(team.ItemId);

            stored.Location = new WorkforceLocation { Address = "1 City Hall Square, Boston" };
            await teams.UpdateAsync(stored);
        });

        var request = await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<GeocodeRequestStore>().GetAllAsync()), commit: false);

        Assert.Equal(WorkforceGeocodeTargets.TeamLocation, request.TargetType);

        Assert.Equal(1, await harness.RunAsync(services => services.GetRequiredService<IGeocodeQueue>().ProcessDueAsync()));

        var located = await harness.RunAsync(async services => await services.GetRequiredService<ITeamManager>().FindByIdAsync(team.ItemId), commit: false);

        Assert.Equal(42.3603, located.Location.Latitude);
        Assert.Equal(-71.0580, located.Location.Longitude);

        // The coordinates are there now, so saving again asks for nothing more.
        Assert.Equal(0, await harness.RunAsync(services => services.GetRequiredService<IGeocodeQueue>().ProcessDueAsync()));
        Assert.Equal(GeocodeRequestStatus.Done, (await harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<GeocodeRequestStore>().GetAllAsync()), commit: false)).Status);
    }
}
