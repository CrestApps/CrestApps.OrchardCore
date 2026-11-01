using CrestApps.OrchardCore.Geo.Models;

namespace CrestApps.OrchardCore.Geo.Services;

/// <summary>
/// Turns addresses into points and points into addresses.
/// </summary>
public interface IGeocodingProvider
{
    /// <summary>
    /// Gets the provider's name, as settings refer to it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to people choosing a provider.
    /// </summary>
    string DisplayName => Name;

    /// <summary>
    /// Finds the places that match a query.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The candidates, best first.</returns>
    Task<IReadOnlyList<GeocodeCandidate>> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the address at a point.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The candidates, best first.</returns>
    Task<IReadOnlyList<GeocodeCandidate>> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives travel times and distances between points.
/// </summary>
public interface ITravelTimeProvider
{
    /// <summary>
    /// Gets the provider's name, as settings refer to it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to people choosing a provider.
    /// </summary>
    string DisplayName => Name;

    /// <summary>
    /// Gives the leg from each origin to each destination.
    /// </summary>
    /// <param name="origins">The origins.</param>
    /// <param name="destinations">The destinations.</param>
    /// <param name="departureUtc">When the trips start, for traffic.</param>
    /// <param name="options">The options.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matrix.</returns>
    Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the longest the provider's terms let its answers be kept, or <see langword="null"/> for the site's setting.
    /// <see cref="TimeSpan.Zero"/> keeps nothing.
    /// </summary>
    TimeSpan? MaxCacheDuration => null;
}

/// <summary>
/// Gives the route through points, for drawing on a map.
/// </summary>
public interface IRouteProvider
{
    /// <summary>
    /// Gets the provider's name, as settings refer to it.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the name shown to people choosing a provider.
    /// </summary>
    string DisplayName => Name;

    /// <summary>
    /// Gives the route through the points, in order.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The route.</returns>
    Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default);
}

/// <summary>
/// The site's geography: the providers its settings choose, cached travel times, and the built-in estimate when a
/// provider is missing or fails.
/// </summary>
public interface IGeoService
{
    /// <summary>
    /// Geocodes with the site's provider. Provider errors are thrown, so callers can retry.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The answer; it names no provider when none is set up.</returns>
    Task<GeocodeResponse> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the address at a point with the site's provider.
    /// </summary>
    /// <param name="point">The point.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The answer; it names no provider when none is set up.</returns>
    Task<GeocodeResponse> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives travel times, cached, from the site's provider, or the built-in estimate when it is missing or fails.
    /// </summary>
    /// <param name="origins">The origins.</param>
    /// <param name="destinations">The destinations.</param>
    /// <param name="departureUtc">When the trips start.</param>
    /// <param name="options">The options, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The matrix.</returns>
    Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gives a route from the site's provider, or straight lines when it is missing or fails.
    /// </summary>
    /// <param name="points">The points.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The route.</returns>
    Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default);
}

/// <summary>
/// Gives the site's Geo settings.
/// </summary>
public interface IGeoSettingsService
{
    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    Task<GeoSettings> GetAsync();
}

/// <summary>
/// Finds the territory of a point or postal code.
/// </summary>
public interface ITerritoryResolver
{
    /// <summary>
    /// Finds the territory: the smallest shape the point is in, else the territory with the postal code.
    /// </summary>
    /// <param name="point">The point, or <see langword="null"/>.</param>
    /// <param name="postalCode">The postal code, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The match, or <see langword="null"/>.</returns>
    Task<TerritoryMatch> ResolveAsync(GeoPoint? point, string postalCode, CancellationToken cancellationToken = default);
}

/// <summary>
/// Geocodes addresses in the background, with retries, and writes each point back through its target.
/// </summary>
public interface IGeocodeQueue
{
    /// <summary>
    /// Asks for a record's address to be geocoded, replacing any request for the same record.
    /// </summary>
    /// <param name="targetType">The kind of record, as its <see cref="IGeocodeTarget"/> names it.</param>
    /// <param name="targetId">The record.</param>
    /// <param name="query">What to geocode.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A task that completes when the request is saved.</returns>
    Task EnqueueAsync(string targetType, string targetId, GeocodeQuery query, CancellationToken cancellationToken = default);

    /// <summary>
    /// Works on the requests that are due.
    /// </summary>
    /// <param name="max">The most requests to work on.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many were worked on.</returns>
    Task<int> ProcessDueAsync(int max = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes every waiting request due now, such as when a geocoding provider is chosen.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>How many requests were waiting.</returns>
    Task<int> WakeAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Writes a geocoded point back to the kind of record it was asked for.
/// </summary>
public interface IGeocodeTarget
{
    /// <summary>
    /// Gets the kind of record.
    /// </summary>
    string TargetType { get; }

    /// <summary>
    /// Writes the point back.
    /// </summary>
    /// <param name="targetId">The record.</param>
    /// <param name="candidate">The place found.</param>
    /// <param name="source">The provider that found it.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns><see langword="false"/> when the record is gone.</returns>
    Task<bool> ApplyAsync(string targetId, GeocodeCandidate candidate, string source, CancellationToken cancellationToken = default);
}
