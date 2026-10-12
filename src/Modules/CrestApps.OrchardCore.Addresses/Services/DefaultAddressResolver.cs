using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Indexes;
using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Default <see cref="IAddressResolver"/> that reads the <c>AddressPart</c> selectors and reduces each
/// referenced geographic area to its stable money-safe code (or display name) using the shared
/// <see cref="GeographicAreaIndex"/>, so no full content items have to be loaded.
/// </summary>
public sealed class DefaultAddressResolver : IAddressResolver
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAddressResolver"/> class.
    /// </summary>
    /// <param name="session">The YesSql session used to query the geographic area index.</param>
    public DefaultAddressResolver(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async ValueTask<Address> ResolveAsync(ContentItem contentItem)
    {
        JsonNode content = contentItem?.Content;
        var addressPart = content?[AddressConstants.AddressPart];

        if (addressPart is null)
        {
            return new Address();
        }

        var pickerFields = new[] { "Country", "Region", "County", "City", "District" };

        var contentItemIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var pickerField in pickerFields)
        {
            var contentItemId = GetFirstReferencedId(addressPart, pickerField);

            if (!string.IsNullOrEmpty(contentItemId))
            {
                contentItemIds.Add(contentItemId);
            }
        }

        var resolved = new Dictionary<string, GeographicAreaIndex>(StringComparer.Ordinal);

        if (contentItemIds.Count > 0)
        {
            var areas = await _session.QueryIndex<GeographicAreaIndex>(index =>
                    index.ContentItemId.IsIn(contentItemIds) && index.Published)
                .ListAsync();

            foreach (var area in areas)
            {
                resolved.TryAdd(area.ContentItemId, area);
            }
        }

        return BuildAddress(addressPart, resolved);
    }

    /// <summary>
    /// Builds a money-safe <see cref="Address"/> from the supplied address part and the geographic area index
    /// rows that were already resolved.
    /// </summary>
    /// <param name="addressPart">The JSON of the <c>AddressPart</c> to read the selectors, street lines, contact fields, and postal code from.</param>
    /// <param name="resolvedAreas">The resolved geographic areas keyed by their content item identifier.</param>
    /// <returns>The resolved money-safe address. Never <see langword="null"/>.</returns>
    internal static Address BuildAddress(JsonNode addressPart, IReadOnlyDictionary<string, GeographicAreaIndex> resolvedAreas)
    {
        return new Address
        {
            Name = ReadText(addressPart, "Name"),
            Company = ReadText(addressPart, "Company"),
            AddressLine1 = ReadText(addressPart, "AddressLine1"),
            AddressLine2 = ReadText(addressPart, "AddressLine2"),
            Country = NormalizeCountry(ResolveComponent(addressPart, "Country", resolvedAreas)),
            Region = ResolveComponent(addressPart, "Region", resolvedAreas),
            County = ResolveComponent(addressPart, "County", resolvedAreas),
            City = ResolveComponent(addressPart, "City", resolvedAreas),
            District = ResolveComponent(addressPart, "District", resolvedAreas),
            PostalCode = ReadText(addressPart, "PostalCode"),
            Phone = ReadText(addressPart, "Phone"),
        };
    }

    private static string ResolveComponent(
        JsonNode addressPart,
        string pickerField,
        IReadOnlyDictionary<string, GeographicAreaIndex> resolvedAreas)
    {
        var contentItemId = GetFirstReferencedId(addressPart, pickerField);

        if (string.IsNullOrEmpty(contentItemId) || !resolvedAreas.TryGetValue(contentItemId, out var area) || area is null)
        {
            return null;
        }

        var code = area.Code?.Trim();

        if (!string.IsNullOrEmpty(code))
        {
            return code;
        }

        return string.IsNullOrWhiteSpace(area.DisplayText)
            ? null
            : area.DisplayText.Trim();
    }

    // Country codes are normalized to upper case so a resolved snapshot always carries a canonical
    // ISO-style code (for example "us" becomes "US"). A longer value (a display name used as a fallback
    // when no code is configured) is left untouched so it is not mangled into upper case.
    private static string NormalizeCountry(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= 3
            ? value.ToUpperInvariant()
            : value;
    }

    private static string GetFirstReferencedId(JsonNode addressPart, string pickerField)
    {
        if (addressPart?[pickerField]?["ContentItemIds"] is not JsonArray ids)
        {
            return null;
        }

        foreach (var id in ids)
        {
            var value = id?.GetValue<string>()?.Trim();

            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string ReadText(JsonNode addressPart, string fieldName)
    {
        var value = addressPart?[fieldName]?["Text"]?.GetValue<string>()?.Trim();

        return string.IsNullOrEmpty(value) ? null : value;
    }
}
