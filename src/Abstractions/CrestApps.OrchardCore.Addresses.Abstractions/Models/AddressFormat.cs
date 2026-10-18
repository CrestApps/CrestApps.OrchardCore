namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// How one country writes and checks an address: which components it uses, in what order, which it requires, what
/// they are called, what its postal codes look like and which regions it has.
/// </summary>
public sealed class AddressFormat
{
    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 code of the country, or <see langword="null"/> for the generic format.
    /// </summary>
    public string CountryCode { get; init; }

    /// <summary>
    /// Gets the lines of the address in the country's own order. The country itself is not part of it.
    /// </summary>
    public IReadOnlyList<AddressLayoutLine> Layout { get; init; } = [];

    /// <summary>
    /// Gets the order for an address written in Latin letters, for a country that writes its own script in a
    /// different order (Japan, China, Korea), or <see langword="null"/>.
    /// </summary>
    public IReadOnlyList<AddressLayoutLine> LatinLayout { get; init; }

    /// <summary>
    /// Gets the components that must be filled in, besides the country.
    /// </summary>
    public IReadOnlySet<AddressComponent> Required { get; init; } = new HashSet<AddressComponent>();

    /// <summary>
    /// Gets the components the country's post wants in capitals on an envelope.
    /// </summary>
    public IReadOnlySet<AddressComponent> Uppercase { get; init; } = new HashSet<AddressComponent>();

    /// <summary>
    /// Gets the pattern a whole postal code matches, or <see langword="null"/> when the country has none.
    /// </summary>
    public string PostalCodePattern { get; init; }

    /// <summary>
    /// Gets examples of the country's postal codes, to show as a hint.
    /// </summary>
    public IReadOnlyList<string> PostalCodeExamples { get; init; } = [];

    /// <summary>
    /// Gets what the region is called: one of <see cref="AddressLabels"/>'s region labels, such as <c>state</c> or
    /// <c>prefecture</c>; <c>province</c> by default.
    /// </summary>
    public string RegionLabel { get; init; }

    /// <summary>
    /// Gets what the city is called: <c>city</c> (the default), <c>post_town</c>, <c>suburb</c> or <c>district</c>.
    /// </summary>
    public string CityLabel { get; init; }

    /// <summary>
    /// Gets what the dependent locality is called, such as <c>suburb</c> (the default), <c>district</c> or
    /// <c>neighborhood</c>.
    /// </summary>
    public string DependentLocalityLabel { get; init; }

    /// <summary>
    /// Gets what the postal code is called: <c>postal</c> (the default), <c>zip</c>, <c>pin</c> or <c>eircode</c>.
    /// </summary>
    public string PostalCodeLabel { get; init; }

    /// <summary>
    /// Gets the country's regions. When there are any, a region must be one of them.
    /// </summary>
    public IReadOnlyList<AddressRegion> Regions { get; init; } = [];

    /// <summary>
    /// Whether the country's address uses the component.
    /// </summary>
    public bool Uses(AddressComponent component)
        => component == AddressComponent.Country || Layout.Any(line => line.Components.Contains(component));

    /// <summary>
    /// The region whose code, key, name or Latin name is <paramref name="value"/>, in that order of preference and
    /// ignoring case, or <see langword="null"/>.
    /// </summary>
    public AddressRegion FindRegion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        value = value.Trim();

        // Codes first across every region: in some countries one region's key is another's code.
        return Regions.FirstOrDefault(region => Matches(region.Code, value))
            ?? Regions.FirstOrDefault(region => Matches(region.Key, value))
            ?? Regions.FirstOrDefault(region => Matches(region.Name, value))
            ?? Regions.FirstOrDefault(region => Matches(region.LatinName, value));
    }

    private static bool Matches(string candidate, string value)
        => candidate is not null && string.Equals(candidate, value, StringComparison.OrdinalIgnoreCase);
}
