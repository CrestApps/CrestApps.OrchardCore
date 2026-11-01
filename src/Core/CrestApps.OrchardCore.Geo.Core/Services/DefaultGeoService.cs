using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Geo.Core.Services;

/// <summary>
/// Picks the providers the settings name, caches travel times, and falls back to the built-in estimate when a
/// travel-time or route provider is missing or fails.
/// </summary>
public sealed class DefaultGeoService : IGeoService
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IEnumerable<IGeocodingProvider> _geocoders;
    private readonly IEnumerable<ITravelTimeProvider> _travelProviders;
    private readonly IEnumerable<IRouteProvider> _routeProviders;
    private readonly HaversineTravelTimeProvider _haversine;
    private readonly IGeoSettingsService _settingsService;
    private readonly IDistributedCache _cache;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultGeoService"/> class.
    /// </summary>
    /// <param name="geocoders">The geocoding providers.</param>
    /// <param name="travelProviders">The travel-time providers.</param>
    /// <param name="routeProviders">The route providers.</param>
    /// <param name="haversine">The built-in estimate.</param>
    /// <param name="settingsService">The settings service.</param>
    /// <param name="cache">The distributed cache.</param>
    /// <param name="logger">The logger.</param>
    public DefaultGeoService(
        IEnumerable<IGeocodingProvider> geocoders,
        IEnumerable<ITravelTimeProvider> travelProviders,
        IEnumerable<IRouteProvider> routeProviders,
        HaversineTravelTimeProvider haversine,
        IGeoSettingsService settingsService,
        IDistributedCache cache,
        ILogger<DefaultGeoService> logger)
    {
        _geocoders = geocoders;
        _travelProviders = travelProviders;
        _routeProviders = routeProviders;
        _haversine = haversine;
        _settingsService = settingsService;
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<GeocodeResponse> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var provider = Find(_geocoders, (await _settingsService.GetAsync()).GeocodingProvider, item => item.Name);

        if (provider is null)
        {
            return new GeocodeResponse();
        }

        var candidates = await provider.GeocodeAsync(query, cancellationToken);

        return new GeocodeResponse { Provider = provider.Name, Candidates = [.. candidates.Where(candidate => candidate.Point.IsValid).OrderByDescending(candidate => candidate.Confidence)] };
    }

    /// <inheritdoc/>
    public async Task<GeocodeResponse> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
    {
        var provider = Find(_geocoders, (await _settingsService.GetAsync()).GeocodingProvider, item => item.Name);

        if (provider is null)
        {
            return new GeocodeResponse();
        }

        return new GeocodeResponse { Provider = provider.Name, Candidates = await provider.ReverseGeocodeAsync(point, cancellationToken) };
    }

    /// <inheritdoc/>
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        if (origins.Count == 0 || destinations.Count == 0)
        {
            return new TravelMatrix { Provider = _haversine.Name, Legs = [.. origins.Select(_ => (IReadOnlyList<TravelLeg>)[])] };
        }

        var settings = await _settingsService.GetAsync();
        var provider = Find(_travelProviders.Where(item => item is not HaversineTravelTimeProvider), settings.TravelTimeProvider, item => item.Name);

        if (provider is null)
        {
            return await _haversine.GetMatrixAsync(origins, destinations, departureUtc, options, cancellationToken);
        }

        var key = CacheKey(provider.Name, origins, destinations, departureUtc, options);
        var cached = await _cache.GetStringAsync(key, cancellationToken);

        if (cached is not null)
        {
            return JsonSerializer.Deserialize<CachedMatrix>(cached, _jsonOptions).ToMatrix(provider.Name);
        }

        try
        {
            var matrix = await provider.GetMatrixAsync(origins, destinations, departureUtc, options, cancellationToken);

            await _cache.SetStringAsync(key, JsonSerializer.Serialize(CachedMatrix.From(matrix), _jsonOptions), new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(Math.Max(1, settings.MatrixCacheMinutes)),
            }, cancellationToken);

            return matrix;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The travel-time provider {Provider} failed; using the built-in estimate.", provider.Name.SanitizeLogValue());

            return await _haversine.GetMatrixAsync(origins, destinations, departureUtc, options, cancellationToken);
        }
    }

    /// <inheritdoc/>
    public async Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        var provider = Find(_routeProviders.Where(item => item is not HaversineTravelTimeProvider), (await _settingsService.GetAsync()).RouteProvider, item => item.Name);

        if (provider is null || points.Count < 2)
        {
            return await _haversine.GetRouteAsync(points, cancellationToken);
        }

        try
        {
            return await provider.GetRouteAsync(points, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "The route provider {Provider} failed; drawing straight lines.", provider.Name.SanitizeLogValue());

            return await _haversine.GetRouteAsync(points, cancellationToken);
        }
    }

    private static T Find<T>(IEnumerable<T> providers, string name, Func<T, string> nameOf)
        where T : class
        => string.IsNullOrEmpty(name) ? null : providers.FirstOrDefault(provider => string.Equals(nameOf(provider), name, StringComparison.OrdinalIgnoreCase));

    // Points are rounded to about 10 meters and departures to the quarter hour, so near repeats share an entry.
    private static string CacheKey(string provider, IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options)
    {
        var builder = new StringBuilder();

        foreach (var point in origins.Concat([new GeoPoint(91, 0)]).Concat(destinations))
        {
            builder.Append(CultureInfo.InvariantCulture, $"{point.Latitude:0.0000},{point.Longitude:0.0000};");
        }

        builder.Append(CultureInfo.InvariantCulture, $"{departureUtc.Ticks / TimeSpan.FromMinutes(15).Ticks}|{options?.SpeedKmh}");

        return $"geo:matrix:{provider}:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))}";
    }

    private sealed class CachedMatrix
    {
        public double[][] Seconds { get; set; }

        public double[][] Meters { get; set; }

        public static CachedMatrix From(TravelMatrix matrix) => new()
        {
            Seconds = [.. matrix.Legs.Select(row => row.Select(leg => leg.Duration.TotalSeconds).ToArray())],
            Meters = [.. matrix.Legs.Select(row => row.Select(leg => leg.DistanceMeters).ToArray())],
        };

        public TravelMatrix ToMatrix(string provider) => new()
        {
            Provider = provider,
            Legs = [.. Seconds.Select((row, i) => (IReadOnlyList<TravelLeg>)[.. row.Select((seconds, j) => new TravelLeg(TimeSpan.FromSeconds(seconds), Meters[i][j]))])],
        };
    }
}
