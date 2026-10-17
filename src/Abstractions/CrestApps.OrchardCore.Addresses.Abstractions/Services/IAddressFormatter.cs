using CrestApps.OrchardCore.Addresses.Models;

namespace CrestApps.OrchardCore.Addresses.Services;

/// <summary>
/// Prints an address the way its country's post expects.
/// </summary>
public interface IAddressFormatter
{
    /// <summary>
    /// The address as lines, in its country's order, skipping empty parts and parts the country does not use (Japan,
    /// for one, writes the city in the street lines). An address written in Latin letters for a country with a
    /// different script follows that country's Latin order.
    /// </summary>
    ValueTask<IReadOnlyList<string>> GetLinesAsync(Address address, AddressFormatOptions options = null);
}

public sealed class AddressFormatOptions
{
    public static readonly AddressFormatOptions Default = new();

    /// <summary>
    /// Gets whether the last line names the country. Leave it out for an address in the store's own country.
    /// </summary>
    public bool IncludeCountry { get; init; } = true;

    /// <summary>
    /// Gets whether to write the components the country's post wants in capitals, as on an envelope or label.
    /// </summary>
    public bool Envelope { get; init; }
}
