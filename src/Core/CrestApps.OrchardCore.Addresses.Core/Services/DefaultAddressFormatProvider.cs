using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;

namespace CrestApps.OrchardCore.Addresses.Core.Services;

/// <summary>
/// Gives the formats of Google's address data, shipped with the package. Nothing is fetched while the site runs.
/// </summary>
public sealed class DefaultAddressFormatProvider : IAddressFormatProvider
{
    private static readonly Lazy<AddressFormatCatalog> _catalog = new(AddressFormatCatalog.Load);

    public ValueTask<AddressFormat> GetFormatAsync(string countryCode)
        => ValueTask.FromResult(_catalog.Value.Get(countryCode));

    internal static AddressFormatCatalog Catalog
        => _catalog.Value;
}
