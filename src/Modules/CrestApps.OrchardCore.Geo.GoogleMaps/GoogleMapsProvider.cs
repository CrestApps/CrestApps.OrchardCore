using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CrestApps.OrchardCore.Geo.Core;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.GoogleMaps;

/// <summary>
/// Geocoding, travel times and routes from Google Maps. Its travel times are not cached.
/// </summary>
public sealed partial class GoogleMapsProvider : IGeocodingProvider, ITravelTimeProvider, IRouteProvider
{
    /// <summary>
    /// The provider's name.
    /// </summary>
    public const string ProviderName = "GoogleMaps";

    /// <summary>
    /// The named HTTP client.
    /// </summary>
    public const string HttpClientName = "GoogleMaps";

    // The Distance Matrix takes at most 25 origins, 25 destinations and 100 elements a request.
    private const int MaxPerSide = 25;
    private const int MaxElements = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleMapsProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public GoogleMapsProvider(IHttpClientFactory httpClientFactory, ISiteService siteService, IDataProtectionProvider dataProtectionProvider)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _protector = dataProtectionProvider.CreateProtector(GoogleMapsSettings.ProtectionPurpose);
    }

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public string DisplayName => "Google Maps";

    /// <inheritdoc/>
    public TimeSpan? MaxCacheDuration => TimeSpan.Zero;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GeocodeCandidate>> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = query.ToSingleLine();

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var path = $"maps/api/geocode/json?address={Uri.EscapeDataString(text)}";
        var country = query.CountryHint ?? query.Address?.Country;

        if (!string.IsNullOrEmpty(country))
        {
            path += $"&components=country:{Uri.EscapeDataString(country)}";
        }

        return Candidates(await SendAsync(path, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GeocodeCandidate>> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
        => Candidates(await SendAsync($"maps/api/geocode/json?latlng={Point(point)}", cancellationToken));

    /// <inheritdoc/>
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        var rows = origins.Select(_ => new TravelLeg[destinations.Count]).ToArray();
        var departure = departureUtc > DateTime.UtcNow ? new DateTimeOffset(departureUtc, TimeSpan.Zero).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture) : "now";
        var destinationChunk = Math.Min(MaxPerSide, destinations.Count);
        var originChunk = Math.Max(1, Math.Min(MaxPerSide, MaxElements / Math.Max(1, destinationChunk)));

        for (var o = 0; o < origins.Count; o += originChunk)
        {
            var from = origins.Skip(o).Take(originChunk).ToList();

            for (var d = 0; d < destinations.Count; d += destinationChunk)
            {
                var to = destinations.Skip(d).Take(destinationChunk).ToList();
                var answer = await SendAsync($"maps/api/distancematrix/json?origins={Points(from)}&destinations={Points(to)}&departure_time={departure}", cancellationToken);
                var i = 0;

                foreach (var row in answer.GetProperty("rows").EnumerateArray())
                {
                    var j = 0;

                    foreach (var element in row.GetProperty("elements").EnumerateArray())
                    {
                        if (element.GetProperty("status").GetString() != "OK")
                        {
                            throw new GeoProviderException("Google Maps found no route between two of the points.");
                        }

                        var duration = element.TryGetProperty("duration_in_traffic", out var traffic) ? traffic : element.GetProperty("duration");

                        rows[o + i][d + j] = new TravelLeg(TimeSpan.FromSeconds(duration.GetProperty("value").GetDouble()), element.GetProperty("distance").GetProperty("value").GetDouble());
                        j++;
                    }

                    i++;
                }
            }
        }

        return new TravelMatrix { Provider = Name, Legs = rows };
    }

    /// <inheritdoc/>
    public async Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 2)
        {
            throw new GeoProviderException("A route needs at least two points.");
        }

        var path = $"maps/api/directions/json?origin={Point(points[0])}&destination={Point(points[^1])}";

        if (points.Count > 2)
        {
            path += $"&waypoints={Points(points.Skip(1).Take(points.Count - 2))}";
        }

        var route = (await SendAsync(path, cancellationToken)).GetProperty("routes")[0];
        var legs = route.GetProperty("legs").EnumerateArray().ToList();

        return new GeoRoute
        {
            Provider = Name,
            Line = PolylineEncoding.Decode(route.GetProperty("overview_polyline").GetProperty("points").GetString()),
            DistanceMeters = legs.Sum(leg => leg.GetProperty("distance").GetProperty("value").GetDouble()),
            Duration = TimeSpan.FromSeconds(legs.Sum(leg => leg.GetProperty("duration").GetProperty("value").GetDouble())),
            Instructions = [.. legs.SelectMany(leg => leg.GetProperty("steps").EnumerateArray())
                .Select(step => step.TryGetProperty("html_instructions", out var html) ? Tags().Replace(html.GetString() ?? string.Empty, " ").Trim() : null)
                .Where(text => !string.IsNullOrEmpty(text))
                .Select(text => Spaces().Replace(text, " "))],
        };
    }

    private static string Point(GeoPoint point) => string.Create(CultureInfo.InvariantCulture, $"{point.Latitude},{point.Longitude}");

    private static string Points(IEnumerable<GeoPoint> points) => Uri.EscapeDataString(string.Join('|', points.Select(Point)));

    private static List<GeocodeCandidate> Candidates(JsonElement answer)
    {
        var candidates = new List<GeocodeCandidate>();

        foreach (var result in answer.GetProperty("results").EnumerateArray())
        {
            var geometry = result.GetProperty("geometry");
            var location = geometry.GetProperty("location");
            var components = result.TryGetProperty("address_components", out var list) ? list.EnumerateArray().ToList() : [];

            candidates.Add(new GeocodeCandidate
            {
                Point = new GeoPoint(location.GetProperty("lat").GetDouble(), location.GetProperty("lng").GetDouble()),
                Confidence = (geometry.TryGetProperty("location_type", out var type) ? type.GetString() : null) switch
                {
                    "ROOFTOP" => 0.95,
                    "RANGE_INTERPOLATED" => 0.8,
                    "GEOMETRIC_CENTER" => 0.6,
                    _ => 0.4,
                },
                FormattedAddress = result.TryGetProperty("formatted_address", out var formatted) ? formatted.GetString() : null,
                PostalCode = Component(components, "postal_code", "long_name"),
                Country = Component(components, "country", "short_name"),
            });
        }

        return candidates;
    }

    private static string Component(List<JsonElement> components, string type, string name)
        => components
            .Where(component => component.GetProperty("types").EnumerateArray().Any(item => item.GetString() == type))
            .Select(component => component.GetProperty(name).GetString())
            .FirstOrDefault();

    private async Task<JsonElement> SendAsync(string path, CancellationToken cancellationToken)
    {
        var settings = await _siteService.GetSettingsAsync<GoogleMapsSettings>();

        if (string.IsNullOrEmpty(settings.Key))
        {
            throw new GeoProviderException("Google Maps has no API key.");
        }

        using var response = await _httpClientFactory.CreateClient(HttpClientName).GetAsync($"{path}&key={Uri.EscapeDataString(_protector.Unprotect(settings.Key))}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GeoProviderException($"Google Maps answered {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement.Clone();
        var status = root.GetProperty("status").GetString();

        // No match is an answer, not an error.
        if (status is "OK" or "ZERO_RESULTS")
        {
            return root;
        }

        var message = root.TryGetProperty("error_message", out var error) ? $"{status}: {error.GetString()}" : status;

        throw new GeoProviderException($"Google Maps refused the request ({message}).");
    }

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tags();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
