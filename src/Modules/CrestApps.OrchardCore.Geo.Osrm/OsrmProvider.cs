using System.Globalization;
using System.Text.Json;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Geo.Osrm;

/// <summary>
/// Travel times and routes from a self-hosted OSRM server.
/// </summary>
public sealed class OsrmProvider : ITravelTimeProvider, IRouteProvider
{
    /// <summary>
    /// The provider's name.
    /// </summary>
    public const string ProviderName = "Osrm";

    /// <summary>
    /// The named HTTP client.
    /// </summary>
    public const string HttpClientName = "Osrm";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ISiteService _siteService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OsrmProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory.</param>
    /// <param name="siteService">The site service.</param>
    public OsrmProvider(IHttpClientFactory httpClientFactory, ISiteService siteService)
    {
        _httpClientFactory = httpClientFactory;
        _siteService = siteService;
    }

    /// <inheritdoc/>
    public string Name => ProviderName;

    /// <inheritdoc/>
    public string DisplayName => "OSRM";

    /// <inheritdoc/>
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> origins, IReadOnlyList<GeoPoint> destinations, DateTime departureUtc, TravelOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(origins);
        ArgumentNullException.ThrowIfNull(destinations);

        var sources = string.Join(';', Enumerable.Range(0, origins.Count));
        var targets = string.Join(';', Enumerable.Range(origins.Count, destinations.Count));
        var answer = await SendAsync("table", origins.Concat(destinations), $"sources={sources}&destinations={targets}&annotations=duration,distance", cancellationToken);
        var durations = answer.GetProperty("durations").EnumerateArray().ToList();
        var distances = answer.GetProperty("distances").EnumerateArray().ToList();

        return new TravelMatrix
        {
            Provider = Name,
            Legs = [.. durations.Select((row, i) => (IReadOnlyList<TravelLeg>)[.. row.EnumerateArray().Select((cell, j) =>
            {
                var distance = distances[i][j];

                if (cell.ValueKind == JsonValueKind.Null || distance.ValueKind == JsonValueKind.Null)
                {
                    throw new GeoProviderException("OSRM found no route between two of the points.");
                }

                return new TravelLeg(TimeSpan.FromSeconds(cell.GetDouble()), distance.GetDouble());
            })])],
        };
    }

    /// <inheritdoc/>
    public async Task<GeoRoute> GetRouteAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(points);

        var route = (await SendAsync("route", points, "overview=full&geometries=geojson&steps=true", cancellationToken)).GetProperty("routes")[0];

        return new GeoRoute
        {
            Provider = Name,
            DistanceMeters = route.GetProperty("distance").GetDouble(),
            Duration = TimeSpan.FromSeconds(route.GetProperty("duration").GetDouble()),
            Line = [.. route.GetProperty("geometry").GetProperty("coordinates").EnumerateArray().Select(position => new GeoPoint(position[1].GetDouble(), position[0].GetDouble()))],
            Instructions = [.. route.GetProperty("legs").EnumerateArray().SelectMany(leg => leg.GetProperty("steps").EnumerateArray()).Select(Instruction).Where(text => text is not null)],
        };
    }

    /// <summary>
    /// Writes an OSRM step as a sentence.
    /// </summary>
    /// <param name="step">The step.</param>
    /// <returns>The sentence, or <see langword="null"/>.</returns>
    internal static string Instruction(JsonElement step)
    {
        var maneuver = step.GetProperty("maneuver");
        var type = maneuver.GetProperty("type").GetString();
        var modifier = maneuver.TryGetProperty("modifier", out var value) ? value.GetString() : null;
        var name = step.TryGetProperty("name", out var road) ? road.GetString() : null;
        var onto = string.IsNullOrEmpty(name) ? string.Empty : $" onto {name}";

        return type switch
        {
            "depart" => string.IsNullOrEmpty(name) ? "Head out" : $"Head out on {name}",
            "arrive" => "Arrive",
            "turn" or "end of road" or "fork" or "on ramp" or "off ramp" => $"{Capitalize(type == "turn" ? "turn" : $"take the {type}")} {modifier}{onto}".Replace("  ", " ", StringComparison.Ordinal).Trim(),
            "roundabout" or "rotary" => $"Take the roundabout{onto}",
            "continue" or "new name" => string.IsNullOrEmpty(name) ? "Continue" : $"Continue on {name}",
            "merge" => $"Merge{onto}",
            _ => null,
        };
    }

    private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];

    private async Task<JsonElement> SendAsync(string service, IEnumerable<GeoPoint> points, string query, CancellationToken cancellationToken)
    {
        var settings = await _siteService.GetSettingsAsync<OsrmSettings>();

        if (string.IsNullOrEmpty(settings.BaseUrl))
        {
            throw new GeoProviderException("OSRM has no server address.");
        }

        var coordinates = string.Join(';', points.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.Longitude},{point.Latitude}")));
        var profile = string.IsNullOrWhiteSpace(settings.Profile) ? "driving" : settings.Profile;

        using var response = await _httpClientFactory.CreateClient(HttpClientName).GetAsync($"{settings.BaseUrl.TrimEnd('/')}/{service}/v1/{Uri.EscapeDataString(profile)}/{coordinates}?{query}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement.Clone();
        var code = root.TryGetProperty("code", out var value) ? value.GetString() : null;

        if (!response.IsSuccessStatusCode || code != "Ok")
        {
            var message = root.TryGetProperty("message", out var text) ? text.GetString() : code ?? ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);

            throw new GeoProviderException($"OSRM couldn't answer: {message}");
        }

        return root;
    }
}
