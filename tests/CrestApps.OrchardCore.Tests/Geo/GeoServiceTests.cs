using CrestApps.OrchardCore.Geo;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Geo;

/// <summary>
/// The built-in travel estimate, and the geo service caching its provider's answers and falling back when it fails.
/// </summary>
public sealed class GeoServiceTests
{
    private static readonly GeoPoint _boston = new(42.3601, -71.0589);
    private static readonly GeoPoint _cambridge = new(42.3736, -71.1097);

    [Fact]
    public async Task TheBuiltInEstimate_AppliesTheRoadFactorAndSpeed()
    {
        var settings = new GeoSettings { RoadFactor = 1.5, AverageSpeedKmh = 30 };
        var provider = new HaversineTravelTimeProvider(Settings(settings));

        var matrix = await provider.GetMatrixAsync([_boston], [_boston, _cambridge], DateTime.UtcNow, new TravelOptions(), TestContext.Current.CancellationToken);
        var leg = matrix[0, 1];
        var straight = CrestApps.OrchardCore.Geo.Core.GeoMath.DistanceMeters(_boston, _cambridge);

        Assert.Equal(TimeSpan.Zero, matrix[0, 0].Duration);
        Assert.Equal(straight * 1.5, leg.DistanceMeters, 3);
        Assert.Equal(straight * 1.5 / 1000 / 30, leg.Duration.TotalHours, 6);

        var faster = await provider.GetMatrixAsync([_boston], [_cambridge], DateTime.UtcNow, new TravelOptions { SpeedKmh = 60 }, TestContext.Current.CancellationToken);

        Assert.Equal(leg.Duration.TotalSeconds / 2, faster[0, 0].Duration.TotalSeconds, 3);
    }

    [Fact]
    public async Task TheProvidersAnswer_IsCached()
    {
        var provider = new Mock<ITravelTimeProvider>();

        provider.SetupGet(p => p.Name).Returns("Roads");
        provider.Setup(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelMatrix { Provider = "Roads", Legs = [[new TravelLeg(TimeSpan.FromMinutes(12), 6000)]] });

        var service = Service(new GeoSettings { TravelTimeProvider = "Roads" }, provider.Object);
        var departure = new DateTime(2026, 11, 2, 14, 3, 0, DateTimeKind.Utc);

        var first = await service.GetMatrixAsync([_boston], [_cambridge], departure, cancellationToken: TestContext.Current.CancellationToken);
        var second = await service.GetMatrixAsync([_boston], [_cambridge], departure.AddMinutes(5), cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(TimeSpan.FromMinutes(12), first[0, 0].Duration);
        Assert.Equal(TimeSpan.FromMinutes(12), second[0, 0].Duration);
        Assert.Equal("Roads", second.Provider);
        provider.Verify(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task AProviderThatMayNotBeCached_IsAskedEachTime()
    {
        var provider = new Mock<ITravelTimeProvider>();

        provider.SetupGet(p => p.Name).Returns("Roads");
        provider.SetupGet(p => p.MaxCacheDuration).Returns(TimeSpan.Zero);
        provider.Setup(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TravelMatrix { Provider = "Roads", Legs = [[new TravelLeg(TimeSpan.FromMinutes(12), 6000)]] });

        var service = Service(new GeoSettings { TravelTimeProvider = "Roads" }, provider.Object);
        var departure = new DateTime(2026, 11, 2, 14, 3, 0, DateTimeKind.Utc);

        await service.GetMatrixAsync([_boston], [_cambridge], departure, cancellationToken: TestContext.Current.CancellationToken);
        await service.GetMatrixAsync([_boston], [_cambridge], departure, cancellationToken: TestContext.Current.CancellationToken);

        provider.Verify(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AFailingProvider_FallsBackToTheBuiltInEstimate()
    {
        var provider = new Mock<ITravelTimeProvider>();

        provider.SetupGet(p => p.Name).Returns("Roads");
        provider.Setup(p => p.GetMatrixAsync(It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<IReadOnlyList<GeoPoint>>(), It.IsAny<DateTime>(), It.IsAny<TravelOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("down"));

        var matrix = await Service(new GeoSettings { TravelTimeProvider = "Roads" }, provider.Object)
            .GetMatrixAsync([_boston], [_cambridge], DateTime.UtcNow, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GeoConstants.HaversineProviderName, matrix.Provider);
        Assert.True(matrix[0, 0].DistanceMeters > 4000);
    }

    [Fact]
    public async Task Geocoding_WithoutAProvider_SaysSo()
    {
        var response = await Service(new GeoSettings()).GeocodeAsync(new GeocodeQuery { Text = "1 Main Street" }, TestContext.Current.CancellationToken);

        Assert.Null(response.Provider);
        Assert.Empty(response.Candidates);
    }

    private static DefaultGeoService Service(GeoSettings settings, ITravelTimeProvider travel = null)
    {
        var haversine = new HaversineTravelTimeProvider(Settings(settings));
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));

        return new DefaultGeoService([], travel is null ? [haversine] : [haversine, travel], [haversine], haversine, Settings(settings), cache, NullLogger<DefaultGeoService>.Instance);
    }

    private static IGeoSettingsService Settings(GeoSettings settings)
    {
        var service = new Mock<IGeoSettingsService>();

        service.Setup(s => s.GetAsync()).ReturnsAsync(settings);

        return service.Object;
    }
}
