using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Geocodes an address, or finds the address at a point, with the geocoding provider the Maps and Geocoding settings
/// choose, through <see cref="IGeoService"/>. Each call is one the site pays its provider for, so it asks for approval and
/// needs <c>ManageGeoSettings</c>. Provider keys are never accepted or returned: the tool takes no key argument, and a
/// provider's error is relayed with anything that looks like a key hidden.
/// </summary>
internal sealed class GeocodeAddressTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "geocodeAddress";

    /// <summary>
    /// The most places returned.
    /// </summary>
    public const int MaxCandidates = 5;

    private static readonly JsonElement _jsonSchema = JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["address"] = AddressArguments.CreateSchema("The address to find, field by field. Give address or text, or latitude and longitude for the opposite lookup.", includeRecipient: false),
            ["text"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The address or place as one line of text, when it is not given field by field.",
            },
            ["countryHint"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "A two-letter country code that narrows the search, such as US.",
            },
            ["latitude"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] = "The latitude of a point whose address is wanted. Give it with longitude.",
            },
            ["longitude"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] = "The longitude of a point whose address is wanted. Give it with latitude.",
            },
        },
        ["required"] = new JsonArray(),
        ["additionalProperties"] = false,
    });

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Looks up the coordinates of an address, or the address of coordinates, with the configured maps provider. It is a billable provider call and fails when no geocoding provider is set.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageGeoSettings;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var latitude = ReadDouble(arguments, "latitude");
        var longitude = ReadDouble(arguments, "longitude");
        var text = ReadString(arguments, "text");
        var countryHint = ReadString(arguments, "countryHint")?.ToUpperInvariant();
        Address address = null;

        if (ReadJson(arguments, "address") is { } addressJson)
        {
            if (!AddressArguments.TryRead(addressJson, includeRecipient: false, out address, out var error))
            {
                return $"Nothing was looked up: {error}";
            }
        }

        if ((latitude is null) != (longitude is null))
        {
            return "Nothing was looked up: give both latitude and longitude, or neither.";
        }

        var reverse = latitude is not null;
        var point = GeoPoint.From(latitude, longitude);

        if (reverse && point is null)
        {
            return "Nothing was looked up: the latitude must be from -90 to 90 and the longitude from -180 to 180.";
        }

        if (reverse && (address is not null || text is not null))
        {
            return "Nothing was looked up: give either an address (address or text) or a point (latitude and longitude), not both.";
        }

        if (!reverse && address is null && text is null)
        {
            return "Nothing was looked up: give the address in address or text, or a point in latitude and longitude.";
        }

        if (countryHint is not null && countryHint.Length != 2)
        {
            return "Nothing was looked up: countryHint must be a two-letter country code, such as US.";
        }

        var geoService = services.GetRequiredService<IGeoService>();
        GeocodeResponse response;

        try
        {
            response = reverse
                ? await geoService.ReverseGeocodeAsync(point.Value, cancellationToken)
                : await geoService.GeocodeAsync(new GeocodeQuery { Address = address, Text = address is null ? text : null, CountryHint = countryHint ?? address?.Country }, cancellationToken);
        }
        catch (GeoProviderException ex)
        {
            return $"The maps provider could not answer: {HideKeys(ex.Message)}";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            services.GetService<ILogger<GeocodeAddressTool>>()?.LogWarning(ex, "A geocoding lookup from an AI tool failed.");

            return "The maps provider could not be reached or did not answer in time. Try again later, or check its settings under Settings, Maps and Geocoding.";
        }

        if (response.Provider is null)
        {
            return "No geocoding provider is set up, so nothing was looked up. Choose one under Settings, Maps and Geocoding; its key is entered there.";
        }

        var candidates = (response.Candidates ?? [])
            .Take(MaxCandidates)
            .Select(candidate => new
            {
                latitude = candidate.Point.Latitude,
                longitude = candidate.Point.Longitude,
                confidence = Math.Round(candidate.Confidence, 2),
                formattedAddress = candidate.FormattedAddress,
                postalCode = candidate.PostalCode,
                country = candidate.Country,
            })
            .ToList();

        return Json(new
        {
            provider = response.Provider,
            reverse,
            query = reverse ? point.ToString() : address is not null ? new GeocodeQuery { Address = address }.ToSingleLine() : text,
            found = candidates.Count > 0,
            candidates,
            totalCandidates = response.Candidates?.Count ?? 0,
            message = candidates.Count == 0
                ? "The provider found no place for this. Check the address, or give more of it."
                : "Candidates are best first. Use findServiceTerritory with a candidate's latitude and longitude to find its territory.",
        });
    }
}
