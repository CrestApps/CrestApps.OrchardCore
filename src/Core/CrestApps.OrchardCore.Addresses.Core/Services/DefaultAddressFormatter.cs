using System.Text;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

public sealed class DefaultAddressFormatter : IAddressFormatter
{
    private readonly IAddressFormatProvider _formatProvider;
    private readonly ICountryService _countryService;

    public DefaultAddressFormatter(IAddressFormatProvider formatProvider, ICountryService countryService)
    {
        _formatProvider = formatProvider;
        _countryService = countryService;
    }

    public async ValueTask<IReadOnlyList<string>> GetLinesAsync(Address address, AddressFormatOptions options = null)
    {
        ArgumentNullException.ThrowIfNull(address);

        options ??= AddressFormatOptions.Default;

        var format = await _formatProvider.GetFormatAsync(address.Country);

        // Japan, China and Korea write their own script from the largest place down, and Latin letters the other way.
        var latin = format.LatinLayout is not null && IsLatin(address);
        var layout = latin ? format.LatinLayout : format.Layout;
        var region = format.FindRegion(address.Region);
        var regionText = region is null
            ? address.Region
            : latin ? region.LatinName ?? region.Key : region.Key;

        string ValueOf(AddressComponent component)
        {
            var value = component switch
            {
                AddressComponent.Name => address.Name,
                AddressComponent.Company => address.Company,
                AddressComponent.DependentLocality => address.DependentLocality,
                AddressComponent.City => address.City,
                AddressComponent.Region => regionText,
                AddressComponent.PostalCode => address.PostalCode,
                AddressComponent.SortingCode => address.SortingCode,
                AddressComponent.StreetAddress => string.Join(", ", new[] { address.AddressLine1, address.AddressLine2 }.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim())),
                _ => null,
            };

            value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();

            return value is not null && options.Envelope && format.Uppercase.Contains(component)
                ? value.ToUpperInvariant()
                : value;
        }

        var lines = new List<string>();

        foreach (var line in layout)
        {
            // The street lines are a line each when they stand alone, which they do in every country's layout.
            if (line.Tokens.Count == 1 && line.Tokens[0].Component == AddressComponent.StreetAddress)
            {
                AddIfAny(lines, Envelope(address.AddressLine1, AddressComponent.StreetAddress, format, options));
                AddIfAny(lines, Envelope(address.AddressLine2, AddressComponent.StreetAddress, format, options));

                continue;
            }

            AddIfAny(lines, Render(line, ValueOf));
        }

        if (options.IncludeCountry && !string.IsNullOrWhiteSpace(address.Country))
        {
            var code = address.Country.Trim();
            var name = (await _countryService.GetCountriesAsync())
                .FirstOrDefault(country => string.Equals(country.Code, code, StringComparison.OrdinalIgnoreCase))?.Name ?? code.ToUpperInvariant();

            lines.Add(options.Envelope ? name.ToUpperInvariant() : name);
        }

        return lines;
    }

    /// <summary>
    /// Writes a line's components with the text between them. Text before the first component belongs to it, such as
    /// <c>〒</c> or <c>CH-</c>; text between components is written only between two that have a value, so a missing
    /// region never leaves <c>Tokyo, </c> behind and never runs the city into the postal code.
    /// </summary>
    private static string Render(AddressLayoutLine line, Func<AddressComponent, string> valueOf)
    {
        var text = new StringBuilder();
        var leading = new StringBuilder();
        string separator = null;
        var seenComponent = false;
        var wroteComponent = false;
        var lastWasWritten = false;

        foreach (var token in line.Tokens)
        {
            if (token.Component is not { } component)
            {
                if (seenComponent)
                {
                    // The text nearest the next written component is the one that separates it.
                    separator = token.Text;
                }
                else
                {
                    leading.Append(token.Text);
                }

                continue;
            }

            var value = valueOf(component);
            var first = !seenComponent;

            seenComponent = true;
            lastWasWritten = value is not null;

            if (value is null)
            {
                continue;
            }

            if (first)
            {
                text.Append(leading);
            }
            else if (wroteComponent && separator is not null)
            {
                text.Append(separator);
            }

            text.Append(value);
            wroteComponent = true;
            separator = null;
        }

        // Text after the last component belongs to it.
        if (separator is not null && lastWasWritten)
        {
            text.Append(separator);
        }

        return text.ToString().Trim();
    }

    private static string Envelope(string value, AddressComponent component, AddressFormat format, AddressFormatOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        return options.Envelope && format.Uppercase.Contains(component) ? value.ToUpperInvariant() : value;
    }

    private static void AddIfAny(List<string> lines, string line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            lines.Add(line);
        }
    }

    /// <summary>
    /// Whether every letter of the address is a Latin letter.
    /// </summary>
    private static bool IsLatin(Address address)
    {
        foreach (var value in new[] { address.Name, address.Company, address.AddressLine1, address.AddressLine2, address.DependentLocality, address.City, address.Region })
        {
            if (value is null)
            {
                continue;
            }

            foreach (var character in value)
            {
                // Basic Latin through Latin Extended-B, and Latin Extended Additional.
                if (char.IsLetter(character) && character > 'ɏ' && character is < 'Ḁ' or > 'ỿ')
                {
                    return false;
                }
            }
        }

        return true;
    }
}
