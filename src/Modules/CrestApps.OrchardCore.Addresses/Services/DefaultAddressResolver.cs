using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Addresses.Models;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Default <see cref="IAddressResolver"/> that reads the <c>AddressPart</c> selectors, loads the referenced
/// geographic content items, and reduces each one to its stable money-safe code (or display name).
/// </summary>
public sealed class DefaultAddressResolver : IAddressResolver
{
    private readonly IContentManager _contentManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAddressResolver"/> class.
    /// </summary>
    /// <param name="contentManager">The content manager used to load the referenced geographic content items.</param>
    public DefaultAddressResolver(IContentManager contentManager)
    {
        _contentManager = contentManager;
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

        var resolved = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

        foreach (var pickerField in pickerFields)
        {
            var contentItemId = GetFirstReferencedId(addressPart, pickerField);

            if (string.IsNullOrEmpty(contentItemId) || resolved.ContainsKey(contentItemId))
            {
                continue;
            }

            var referenced = await _contentManager.GetAsync(contentItemId);

            if (referenced is not null)
            {
                resolved[contentItemId] = referenced;
            }
        }

        return BuildAddress(addressPart, resolved);
    }

    /// <summary>
    /// Builds a money-safe <see cref="Address"/> from the supplied address part and the referenced geographic
    /// content items that were already loaded.
    /// </summary>
    /// <param name="addressPart">The JSON of the <c>AddressPart</c> to read the selectors and postal code from.</param>
    /// <param name="resolvedItems">The referenced geographic content items keyed by their content item identifier.</param>
    /// <returns>The resolved money-safe address. Never <see langword="null"/>.</returns>
    internal static Address BuildAddress(JsonNode addressPart, IReadOnlyDictionary<string, ContentItem> resolvedItems)
    {
        return new Address
        {
            Country = ResolveComponent(addressPart, "Country", AddressConstants.CountryPart, resolvedItems),
            Region = ResolveComponent(addressPart, "Region", AddressConstants.RegionPart, resolvedItems),
            County = ResolveComponent(addressPart, "County", AddressConstants.CountyPart, resolvedItems),
            City = ResolveComponent(addressPart, "City", AddressConstants.CityPart, resolvedItems),
            District = ResolveComponent(addressPart, "District", AddressConstants.DistrictPart, resolvedItems),
            PostalCode = ReadText(addressPart, "PostalCode"),
        };
    }

    private static string ResolveComponent(
        JsonNode addressPart,
        string pickerField,
        string partName,
        IReadOnlyDictionary<string, ContentItem> resolvedItems)
    {
        var contentItemId = GetFirstReferencedId(addressPart, pickerField);

        if (string.IsNullOrEmpty(contentItemId) || !resolvedItems.TryGetValue(contentItemId, out var item) || item is null)
        {
            return null;
        }

        JsonNode itemContent = item.Content;
        var code = itemContent?[partName]?[AddressConstants.CodeField]?["Text"]?.GetValue<string>()?.Trim();

        if (!string.IsNullOrEmpty(code))
        {
            return code;
        }

        return string.IsNullOrWhiteSpace(item.DisplayText)
            ? null
            : item.DisplayText.Trim();
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
