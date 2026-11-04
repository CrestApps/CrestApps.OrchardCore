using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.AspNetCore.DataProtection;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.AzureMaps;

/// <summary>
/// Geocoding, travel times and routes from Azure Maps.
/// </summary>
public sealed class AzureMapsProvider : IGeocodingProvider, ITravelTimeProvider, IRouteProvider
{
    /// <summary>
    /// The provider's name.
    /// </summary>
    public const string ProviderName = "AzureMaps";

    /// <summary>
    /// The named HTTP client.
    /// </summary>
    public const string HttpClientName = "AzureMaps";

    // The synchronous matrix answers at most this many cells a request.
    private const int MaxCells = 100;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;
    private readonly IDataProtector _protector;

    /// <summary>
    /// Initializes a new instance of the <see cref="AzureMapsProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service.</param>
    /// <param name="dataProtectionProvider">The data protection provider.</param>
    public AzureMapsProvider(IHttpClientFactory httpClientFactory, ISiteService siteService, IDataProtectionProvider dataProtectionProvider)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
        _protector = dataProtectionProvider.CreateProtector(AzureMapsSettings.ProtectionPurpose);
    }

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public string DisplayName => "Azure Maps";

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GeocodeCandidate>> GeocodeAsync(GeocodeQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var text = query.ToSingleLine();

        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var path = $"geocode?api-version=2023-06-01&top=5&query={Uri.EscapeDataString(text)}";

        if (!string.IsNullOrEmpty(query.CountryHint ?? query.Address?.Country))
        {
            path += $"&countryRegion={Uri.EscapeDataString(query.CountryHint ?? query.Address.Country)}";
        }

        return Candidates(await SendAsync(HttpMethod.Get, path, null, cancellationToken));
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<GeocodeCandidate>> ReverseGeocodeAsync(GeoPoint point, CancellationToken cancellationToken = default)
        => Candidates(await SendAsync(HttpMethod.Get, string.Create(CultureInfo.InvariantCulture, $"reverseGeocode?api-version=2023-06-01&coordinates={point.Longitude},{point.Latitude}"), null, cancellationToken));

    /// <inheritdoc/>
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        if (destinations.Count > MaxCells)
        {
            throw new GeoProviderException($"Azure Maps takes at most {MaxCells} destinations at once.");
        }

        var rows = new List<IReadOnlyList<TravelLeg>>();
        var perRequest = Math.Max(1, MaxCells / Math.Max(1, destinations.Count));
        var departAt = departureUtc > DateTime.UtcNow ? departureUtc.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) : "now";

        foreach (var chunk in origins.Chunk(perRequest))
        {
            var body = new
            {
                origins = new { type = "MultiPoint", coordinates = chunk.Select(Position).ToArray() },
                destinations = new { type = "MultiPoint", coordinates = destinations.Select(Position).ToArray() },
            };
            var answer = await SendAsync(HttpMethod.Post, $"route/matrix/sync/json?api-version=1.0&travelMode=car&departAt={Uri.EscapeDataString(departAt)}", body, cancellationToken);

            foreach (var row in answer.GetProperty("matrix").EnumerateArray())
            {
                rows.Add([.. row.EnumerateArray().Select(Cell)]);
            }
        }

        return new TravelMatrix { Provider = Name, Legs = rows };
    }

    /// <inheritdoc/>
    public async Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        var query = string.Join(':', points.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.Latitude},{point.Longitude}")));
        var answer = await SendAsync(HttpMethod.Get, $"route/directions/json?api-version=1.0&instructionsType=text&query={Uri.EscapeDataString(query)}", null, cancellationToken);
        var route = answer.GetProperty("routes")[0];
        var summary = route.GetProperty("summary");

        return new GeoRoute
        {
            Provider = Name,
            DistanceMeters = summary.GetProperty("lengthInMeters").GetDouble(),
            Duration = TimeSpan.FromSeconds(summary.GetProperty("travelTimeInSeconds").GetDouble()),
            Line = [.. route.GetProperty("legs").EnumerateArray().SelectMany(leg => leg.GetProperty("points").EnumerateArray())
                .Select(point => new GeoPoint(point.GetProperty("latitude").GetDouble(), point.GetProperty("longitude").GetDouble()))],
            Instructions = route.TryGetProperty("guidance", out var guidance) && guidance.TryGetProperty("instructions", out var instructions)
                ? [.. instructions.EnumerateArray().Select(item => item.TryGetProperty("message", out var message) ? message.GetString() : null).Where(message => !string.IsNullOrEmpty(message))]
                : [],
        };
    }

    private static double[] Position(GeoPoint point) => [point.Longitude, point.Latitude];

    private static TravelLeg Cell(JsonElement cell)
    {
        if (cell.GetProperty("statusCode").GetInt32() != 200)
        {
            throw new GeoProviderException("Azure Maps found no route between two of the points.");
        }

        var summary = cell.GetProperty("response").GetProperty("routeSummary");

        return new TravelLeg(TimeSpan.FromSeconds(summary.GetProperty("travelTimeInSeconds").GetDouble()), summary.GetProperty("lengthInMeters").GetDouble());
    }

    private static List<GeocodeCandidate> Candidates(JsonElement answer)
    {
        var candidates = new List<GeocodeCandidate>();

        foreach (var feature in answer.GetProperty("features").EnumerateArray())
        {
            var coordinates = feature.GetProperty("geometry").GetProperty("coordinates");
            var properties = feature.GetProperty("properties");
            var address = properties.TryGetProperty("address", out var value) ? value : default;

            candidates.Add(new GeocodeCandidate
            {
                Point = new GeoPoint(coordinates[1].GetDouble(), coordinates[0].GetDouble()),
                Confidence = (properties.TryGetProperty("confidence", out var confidence) ? confidence.GetString() : null) switch
                {
                    "High" => 0.9,
                    "Medium" => 0.6,
                    _ => 0.3,
                },
                FormattedAddress = Text(address, "formattedAddress"),
                PostalCode = Text(address, "postalCode"),
                Country = address.ValueKind == JsonValueKind.Object && address.TryGetProperty("countryRegion", out var country) ? Text(country, "ISO") : null,
            });
        }

        return candidates;
    }

    private static string Text(JsonElement element, string property)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private async Task<JsonElement> SendAsync(HttpMethod method, string path, object body, CancellationToken cancellationToken)
    {
        var settings = await _siteService.GetSettingsAsync<AzureMapsSettings>();

        if (string.IsNullOrEmpty(settings.Key))
        {
            throw new GeoProviderException("Azure Maps has no subscription key.");
        }

        using var request = new HttpRequestMessage(method, path);

        request.Headers.Add("subscription-key", _protector.Unprotect(settings.Key));

        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        using var response = await _httpClientFactory.CreateClient(HttpClientName).SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new GeoProviderException($"Azure Maps answered {(int)response.StatusCode}: {Error(json)}");
        }

        using var document = JsonDocument.Parse(json);

        return document.RootElement.Clone();
    }

    private static string Error(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);

            return document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message) ? message.GetString() : json;
        }
        catch (JsonException)
        {
            return json;
        }
    }
}
