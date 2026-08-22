namespace CrestApps.OrchardCore.Addresses.Models;

/// <summary>
/// Represents a country reference entry used to populate country selectors.
/// </summary>
public sealed class CountryInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CountryInfo"/> class.
    /// </summary>
    /// <param name="code">The ISO 3166-1 alpha-2 country code.</param>
    /// <param name="name">The English display name of the country.</param>
    public CountryInfo(string code, string name)
    {
        Code = code;
        Name = name;
    }

    /// <summary>
    /// Gets the ISO 3166-1 alpha-2 country code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the English display name of the country.
    /// </summary>
    public string Name { get; }
}
