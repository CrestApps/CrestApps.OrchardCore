using System.Text.Json;
using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// Reads an <see cref="Address"/> given as a JSON object, such as an argument of an AI tool, and describes that object
/// as a JSON schema, so every caller that takes an address takes the same fields under the same names.
/// </summary>
/// <remarks>
/// Only the postal fields a person enters are read: coordinates and geocoding details are set by geocoding, never
/// given. Every value is text, and a field the object does not know is refused instead of ignored, so a misspelled
/// field is never silently dropped.
/// </remarks>
public static class AddressArguments
{
    private static readonly (string Name, string Description, bool Recipient)[] _fields =
    [
        ("name", "Who receives mail at the address (a person or attention line).", true),
        ("company", "The company or organization the address belongs to.", true),
        ("addressLine1", "The first street line: street number and name, or PO box.", false),
        ("addressLine2", "The second street line: apartment, suite, unit or building.", false),
        ("dependentLocality", "The part of the city, for countries whose post uses one (suburb, neighbourhood, district).", false),
        ("city", "The city or town.", false),
        ("region", "The state, province or region: its code (CA, ON, 13) or its name.", false),
        ("postalCode", "The postal or ZIP code.", false),
        ("sortingCode", "The sorting code a few countries add after the city, such as the French CEDEX.", false),
        ("country", "The two-letter ISO country code, such as US, GB or JP.", false),
    ];

    /// <summary>
    /// Creates the JSON schema of an address object.
    /// </summary>
    /// <param name="description">The description of the argument.</param>
    /// <param name="includeRecipient">Whether the recipient's name and the company can be given.</param>
    /// <returns>The schema.</returns>
    public static JsonObject CreateSchema(string description, bool includeRecipient = true)
    {
        var properties = new JsonObject();

        foreach (var (name, fieldDescription, recipient) in _fields)
        {
            if (recipient && !includeRecipient)
            {
                continue;
            }

            properties[name] = new JsonObject
            {
                ["type"] = "string",
                ["description"] = fieldDescription,
            };
        }

        return new JsonObject
        {
            ["type"] = "object",
            ["description"] = description,
            ["properties"] = properties,
            ["additionalProperties"] = false,
        };
    }

    /// <summary>
    /// Reads an address from a JSON object.
    /// </summary>
    /// <param name="value">The JSON object.</param>
    /// <param name="includeRecipient">Whether the recipient's name and the company can be given.</param>
    /// <param name="address">The address that was read.</param>
    /// <param name="error">Why the object could not be read, as a sentence.</param>
    /// <returns><see langword="true"/> when the address was read.</returns>
    public static bool TryRead(JsonElement value, bool includeRecipient, out Address address, out string error)
    {
        address = null;
        error = null;

        if (value.ValueKind != JsonValueKind.Object)
        {
            error = "The address must be an object with fields such as addressLine1, city, region, postalCode and country.";

            return false;
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var property in value.EnumerateObject())
        {
            var field = _fields.FirstOrDefault(item => string.Equals(item.Name, property.Name, StringComparison.OrdinalIgnoreCase));

            if (field.Name is null || (field.Recipient && !includeRecipient))
            {
                error = $"'{property.Name}' is not a field of the address. Use only: {string.Join(", ", _fields.Where(item => includeRecipient || !item.Recipient).Select(item => item.Name))}.";

                return false;
            }

            if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                error = $"The address field '{field.Name}' must be text.";

                return false;
            }

            var text = property.Value.GetString()?.Trim();

            if (!string.IsNullOrEmpty(text))
            {
                values[field.Name] = text;
            }
        }

        address = new Address
        {
            Name = values.GetValueOrDefault("name"),
            Company = values.GetValueOrDefault("company"),
            AddressLine1 = values.GetValueOrDefault("addressLine1"),
            AddressLine2 = values.GetValueOrDefault("addressLine2"),
            DependentLocality = values.GetValueOrDefault("dependentLocality"),
            City = values.GetValueOrDefault("city"),
            Region = values.GetValueOrDefault("region"),
            PostalCode = values.GetValueOrDefault("postalCode"),
            SortingCode = values.GetValueOrDefault("sortingCode"),
            Country = values.GetValueOrDefault("country")?.ToUpperInvariant(),
        };

        return true;
    }

    /// <summary>
    /// Writes the postal fields of an address as a JSON-ready object, without coordinates or a phone number.
    /// </summary>
    /// <param name="address">The address.</param>
    /// <returns>The fields that have a value, by name.</returns>
    public static Dictionary<string, string> Describe(Address address)
    {
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);

        if (address is null)
        {
            return fields;
        }

        Add(fields, "name", address.Name);
        Add(fields, "company", address.Company);
        Add(fields, "addressLine1", address.AddressLine1);
        Add(fields, "addressLine2", address.AddressLine2);
        Add(fields, "dependentLocality", address.DependentLocality);
        Add(fields, "city", address.City);
        Add(fields, "region", address.Region);
        Add(fields, "postalCode", address.PostalCode);
        Add(fields, "sortingCode", address.SortingCode);
        Add(fields, "country", address.Country);

        return fields;
    }

    private static void Add(Dictionary<string, string> fields, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            fields[name] = value;
        }
    }
}
