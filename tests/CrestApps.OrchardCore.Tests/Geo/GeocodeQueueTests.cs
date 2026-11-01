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
using OrchardCore.Modules;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// The background geocoding queue: one request per record, retried with growing waits, given up after five failures,
/// waiting while no provider is set up, and written back through the record's target.
/// </summary>
public sealed class GeocodeQueueTests
{
    private static readonly GeoPoint _point = new(42.3601, -71.0589);

    [Fact]
    public async Task AFoundPlace_IsWrittenBack()
    {
        await using var setup = await SetupAsync();

        setup.Geocoder.Setup(g => g.GeocodeAsync(It.IsAny<GeocodeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new GeocodeCandidate { Point = new GeoPoint(1, 1), Confidence = 0.4 }, new GeocodeCandidate { Point = _point, Confidence = 0.9 }]);

        await EnqueueAsync(setup, "1 City Hall Square, Boston");

        Assert.Equal(1, await ProcessAsync(setup));
        Assert.Equal((_point, "Fake"), Assert.Single(setup.Applied));

        var request = await RequestAsync(setup);

        Assert.Equal(GeocodeRequestStatus.Done, request.Status);
        Assert.Equal(_point, request.Result);
        Assert.Equal(0, await ProcessAsync(setup));
    }

    [Fact]
    public async Task Failures_AreRetriedWithGrowingWaits_ThenGivenUp()
    {
        await using var setup = await SetupAsync();

        setup.Geocoder.Setup(g => g.GeocodeAsync(It.IsAny<GeocodeQuery>(), It.IsAny<CancellationToken>())).ThrowsAsync(new HttpRequestException("timeout"));

        await EnqueueAsync(setup, "1 City Hall Square, Boston");

        var waits = new List<TimeSpan>();

        for (var attempt = 1; attempt <= DefaultGeocodeQueue.MaxAttempts; attempt++)
        {
            Assert.Equal(1, await ProcessAsync(setup));

            var request = await RequestAsync(setup);

            if (attempt < DefaultGeocodeQueue.MaxAttempts)
            {
                Assert.Equal(GeocodeRequestStatus.Pending, request.Status);
                waits.Add(request.NextAttemptUtc - setup.Now);

                // Not due yet.
                Assert.Equal(0, await ProcessAsync(setup));
                setup.Now = request.NextAttemptUtc;
            }
            else
            {
                Assert.Equal(GeocodeRequestStatus.Failed, request.Status);
                Assert.Equal("timeout", request.LastError);
            }
        }

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2)], waits);
        Assert.Empty(setup.Applied);
    }

    [Fact]
    public async Task WithoutAProvider_RequestsWait_AndAskingAgainStartsOver()
    {
        await using var setup = await SetupAsync(provider: null);

        await EnqueueAsync(setup, "1 City Hall Square, Boston");
        await ProcessAsync(setup);

        var waiting = await RequestAsync(setup);

        Assert.Equal(GeocodeRequestStatus.Pending, waiting.Status);
        Assert.Equal(0, waiting.Attempts);
        Assert.Equal(setup.Now + DefaultGeocodeQueue.NoProviderWait, waiting.NextAttemptUtc);
        Assert.Equal("No geocoding provider is set up.", waiting.LastError);

        await EnqueueAsync(setup, "2 City Hall Square, Boston");

        var again = await RequestAsync(setup);

        Assert.Equal(waiting.ItemId, again.ItemId);
        Assert.Equal(setup.Now, again.NextAttemptUtc);
        Assert.Equal("2 City Hall Square, Boston", again.Query.Text);
    }

    [Fact]
    public async Task NoMatch_IsGivenUp()
    {
        await using var setup = await SetupAsync();

        setup.Geocoder.Setup(g => g.GeocodeAsync(It.IsAny<GeocodeQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        await EnqueueAsync(setup, "Nowhere");
        await ProcessAsync(setup);

        var request = await RequestAsync(setup);

        Assert.Equal(GeocodeRequestStatus.Failed, request.Status);
        Assert.Equal("No place matches the address.", request.LastError);
    }

    private static Task EnqueueAsync(Setup setup, string text)
        => setup.Harness.RunAsync(services => services.GetRequiredService<IGeocodeQueue>().EnqueueAsync("Office", "office-1", new GeocodeQuery { Text = text }));

    private static Task<int> ProcessAsync(Setup setup)
        => setup.Harness.RunAsync(services => services.GetRequiredService<IGeocodeQueue>().ProcessDueAsync());

    private static Task<GeocodeRequest> RequestAsync(Setup setup)
        => setup.Harness.RunAsync(async services => Assert.Single(await services.GetRequiredService<GeocodeRequestStore>().GetAllAsync()), commit: false);

    private static async Task<Setup> SetupAsync(string provider = "Fake")
    {
        var setup = new Setup();
        var clock = new Mock<IClock>();
        var settings = new Mock<IGeoSettingsService>();
        var target = new Mock<IGeocodeTarget>();

        clock.SetupGet(c => c.UtcNow).Returns(() => setup.Now);
        settings.Setup(s => s.GetAsync()).ReturnsAsync(new GeoSettings { GeocodingProvider = provider });
        setup.Geocoder.SetupGet(g => g.Name).Returns("Fake");
        target.SetupGet(t => t.TargetType).Returns("Office");
        target.Setup(t => t.ApplyAsync("office-1", It.IsAny<GeocodeCandidate>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback((string _, GeocodeCandidate candidate, string source, CancellationToken _) => setup.Applied.Add((candidate.Point, source)))
            .ReturnsAsync(true);

        setup.Harness = await CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSingleton(clock.Object);
                services.AddSingleton(settings.Object);
                services.AddSingleton<IDistributedCache>(new MemoryDistributedCache(Microsoft.Extensions.Options.Options.Create(new MemoryDistributedCacheOptions())));
                services.AddSingleton(setup.Geocoder.Object);
                services.AddSingleton(target.Object);
                services.AddGeoCore();
            },
            GeoIndexSchema.CreateAsync,
            [new ServiceTerritoryIndexProvider(), new GeocodeRequestIndexProvider()],
            [GeoConstants.CollectionName]);

        return setup;
    }

    private sealed class Setup : IAsyncDisposable
    {
        public CalendarTestHarness Harness { get; set; }

        public Mock<IGeocodingProvider> Geocoder { get; } = new();

        public List<(GeoPoint Point, string Source)> Applied { get; } = [];

        public DateTime Now { get; set; } = CalendarTestHarness.Now;

        public ValueTask DisposeAsync() => Harness?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
