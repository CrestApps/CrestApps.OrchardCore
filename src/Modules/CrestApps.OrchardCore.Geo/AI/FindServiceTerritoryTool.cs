using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Geo.Core.Services;
using CrestApps.OrchardCore.Geo.Models;
using CrestApps.OrchardCore.Geo.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo.AI;

/// <summary>
/// Finds the territory of a street address, a point or a postal code with the territory resolver, as the Territories
/// page's finder does: the smallest shape the point is in, else the territory with the postal code. A street address is
/// first geocoded with the provider the Maps and Geocoding settings choose, through <see cref="IGeoService"/>; that is a
/// call the site pays its provider for, so it also needs <c>ManageGeoSettings</c>, the permission of those settings.
/// Provider keys are never accepted or returned: a provider's error is relayed with anything that looks like a key hidden.
/// </summary>
internal sealed class FindServiceTerritoryTool : GeoAIToolBase
{
    /// <summary>
    /// The name of the tool.
    /// </summary>
    public const string TheName = "findServiceTerritory";

    private static readonly JsonElement _jsonSchema = JsonSerializer.SerializeToElement(new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["address"] = AddressArguments.CreateSchema("A street address to find the territory of, field by field. It is geocoded with the configured maps provider first. Give address, text, a point or a postal code.", includeRecipient: false),
            ["text"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "A street address or place as one line of text, when it is not given field by field. It is geocoded first.",
            },
            ["countryHint"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "A two-letter country code that narrows the geocoding of text, such as US.",
            },
            ["latitude"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] = "The latitude in degrees, from -90 to 90. Give it with longitude, instead of an address.",
            },
            ["longitude"] = new JsonObject
            {
                ["type"] = "number",
                ["description"] = "The longitude in degrees, from -180 to 180. Give it with latitude, instead of an address.",
            },
            ["postalCode"] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = "The postal code, used when the point is in no territory's shape or no point or address is given.",
            },
        },
        ["required"] = new JsonArray(),
        ["additionalProperties"] = false,
    });

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds which service territory covers a street address, a latitude/longitude or a postal code. A street address is geocoded first with the configured maps provider (a billable call that needs the Manage Geo settings permission), and the result returns the point it found.";

    /// <inheritdoc/>
    public override JsonElement JsonSchema => _jsonSchema;

    /// <inheritdoc/>
    protected override Permission Permission => GeoPermissions.ManageTerritories;

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, CancellationToken cancellationToken)
    {
        var latitude = ReadDouble(arguments, "latitude");
        var longitude = ReadDouble(arguments, "longitude");
        var postalCode = ReadString(arguments, "postalCode");
        var text = ReadString(arguments, "text");
        var countryHint = ReadString(arguments, "countryHint")?.ToUpperInvariant();
        Address address = null;

        if (ReadJson(arguments, "address") is { } addressJson)
        {
            if (!AddressArguments.TryRead(addressJson, includeRecipient: false, out address, out var error))
            {
                return $"Nothing was searched: {error}";
            }
        }

        if ((latitude is null) != (longitude is null))
        {
            return "Nothing was searched: give both latitude and longitude, or neither.";
        }

        var point = GeoPoint.From(latitude, longitude);

        if (latitude is not null && point is null)
        {
            return "Nothing was searched: the latitude must be from -90 to 90 and the longitude from -180 to 180.";
        }

        var byAddress = address is not null || text is not null;

        if (byAddress && point is not null)
        {
            return "Nothing was searched: give either an address (address or text) or a point (latitude and longitude), not both.";
        }

        if (!byAddress && point is null && postalCode is null)
        {
            return "Nothing was searched: give a street address (address or text), a latitude and longitude, or a postal code.";
        }

        if (countryHint is not null && countryHint.Length != 2)
        {
            return "Nothing was searched: countryHint must be a two-letter country code, such as US.";
        }

        object geocoded = null;

        if (byAddress)
        {
            var authorizationService = services.GetRequiredService<IAuthorizationService>();

            if (!await authorizationService.AuthorizeAsync(GetCaller(services), GeoPermissions.ManageGeoSettings))
            {
                return "Nothing was searched: finding the territory of a street address geocodes it with the site's maps provider, which needs the Manage Geo settings permission. Give a latitude and longitude or a postal code instead.";
            }

            GeocodeResponse response;

            try
            {
                response = await services.GetRequiredService<IGeoService>().GeocodeAsync(new GeocodeQuery
                {
                    Address = address,
                    Text = address is null ? text : null,
                    CountryHint = countryHint ?? address?.Country,
                }, cancellationToken);
            }
            catch (GeoProviderException ex)
            {
                return $"Nothing was searched: the maps provider could not answer: {HideKeys(ex.Message)}";
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
            {
                services.GetService<ILogger<FindServiceTerritoryTool>>()?.LogWarning(ex, "A geocoding lookup from an AI tool failed.");

                return "Nothing was searched: the maps provider could not be reached or did not answer in time. Try again later, or give a postal code.";
            }

            if (response.Provider is null)
            {
                return "Nothing was searched: no geocoding provider is set up, so the address could not be placed. Choose one under Settings, Maps and Geocoding (its key is entered there), or give a postal code.";
            }

            var best = response.Candidates is { Count: > 0 } candidates
                ? candidates[0]
                : null;

            if (best is null)
            {
                return Json(new
                {
                    found = false,
                    query = address is not null ? new GeocodeQuery { Address = address }.ToSingleLine() : text,
                    provider = response.Provider,
                    message = "The maps provider found no place for this address. Check the address, give more of it, or give a postal code.",
                });
            }

            point = best.Point;
            postalCode ??= best.PostalCode ?? address?.PostalCode;
            geocoded = new
            {
                provider = response.Provider,
                formattedAddress = best.FormattedAddress,
                latitude = best.Point.Latitude,
                longitude = best.Point.Longitude,
                confidence = Math.Round(best.Confidence, 2),
                postalCode = best.PostalCode,
                otherCandidates = Math.Max(0, (response.Candidates?.Count ?? 0) - 1),
            };
        }

        var match = await services.GetRequiredService<ITerritoryResolver>().ResolveAsync(point, postalCode, cancellationToken);

        if (match is null)
        {
            return Json(new
            {
                found = false,
                geocoded,
                latitude = point?.Latitude,
                longitude = point?.Longitude,
                postalCode,
                message = "No service territory covers this point or postal code.",
            });
        }

        var all = await services.GetRequiredService<ServiceTerritoryManager>().GetAllAsync(cancellationToken);
        var names = all.ToDictionary(item => item.ItemId, item => item.Name, StringComparer.Ordinal);
        var sources = await TerritoryToolDescriptions.ReadMemberSourcesAsync(services.GetServices<ITerritoryMemberSource>(), cancellationToken);

        return Json(new
        {
            found = true,
            matchedBy = match.MatchedBy.ToString(),
            geocoded,
            territory = TerritoryToolDescriptions.Describe(match.Territory, names, sources),
            message = match.MatchedBy == TerritoryMatchKind.Shape
                ? $"The point is inside the shape of '{match.Territory.Name}'."
                : $"The postal code belongs to '{match.Territory.Name}'.",
        });
    }
}
