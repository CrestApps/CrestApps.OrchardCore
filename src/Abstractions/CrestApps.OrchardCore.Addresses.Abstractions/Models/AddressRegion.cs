namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// A state, province, prefecture or other first-level region of a country.
/// </summary>
public sealed class AddressRegion
{
    /// <summary>
    /// Gets the code an <see cref="Address"/> stores: the ISO 3166-2 code without the country, or <see cref="Key"/>
    /// when the region has none.
    /// </summary>
    public string Code { get; init; }

    /// <summary>
    /// Gets what the country's post writes, such as <c>CA</c>, <c>Co. Dublin</c> or <c>東京都</c>.
    /// </summary>
    public string Key { get; init; }

    /// <summary>
    /// Gets the region's name in the country's language.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// Gets the region's name in Latin letters, for a country that writes it in another script.
    /// </summary>
    public string LatinName { get; init; }

    /// <summary>
    /// Gets the pattern the start of the region's postal codes matches, when the country has one.
    /// </summary>
    public string PostalCodePrefix { get; init; }
}
