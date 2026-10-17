using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Gives each country's address format.
/// </summary>
public interface IAddressFormatProvider
{
    /// <summary>
    /// The format of the country with the ISO 3166-1 alpha-2 code <paramref name="countryCode"/>, or the generic
    /// format when the country is unknown or has no data. Never <see langword="null"/>.
    /// </summary>
    ValueTask<AddressFormat> GetFormatAsync(string countryCode);
}
