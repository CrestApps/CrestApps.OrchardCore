using System.Text.Json.Nodes;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Addresses;

/// <summary>
/// Reads and writes the ISO code of a Country content item, kept in the text field <see cref="AddressConstants.CodeField"/>
/// of its <see cref="AddressConstants.CountryPart"/>.
/// </summary>
public static class CountryContentItemExtensions
{
    private const string TextProperty = "Text";

    /// <summary>
    /// Returns the country's ISO code as it is stored, trimmed, or <see langword="null"/> when it has none.
    /// </summary>
    /// <param name="contentItem">The Country content item.</param>
    public static string GetCountryCode(this ContentItem contentItem)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        JsonNode content = contentItem.Content;

        return content?[AddressConstants.CountryPart]?[AddressConstants.CodeField]?[TextProperty]?.GetValue<string>()?.Trim();
    }

    /// <summary>
    /// Sets the country's ISO code.
    /// </summary>
    /// <param name="contentItem">The Country content item.</param>
    /// <param name="countryCode">The ISO code to store.</param>
    public static void SetCountryCode(this ContentItem contentItem, string countryCode)
    {
        ArgumentNullException.ThrowIfNull(contentItem);

        JsonNode content = contentItem.Content;

        if (content[AddressConstants.CountryPart] is not JsonObject part)
        {
            part = [];
            content[AddressConstants.CountryPart] = part;
        }

        if (part[AddressConstants.CodeField] is not JsonObject field)
        {
            field = [];
            part[AddressConstants.CodeField] = field;
        }

        field[TextProperty] = countryCode;
    }
}
