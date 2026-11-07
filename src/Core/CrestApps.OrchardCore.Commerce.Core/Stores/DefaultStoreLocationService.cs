using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Addresses.Services;
using CrestApps.OrchardCore.Commerce.Stores;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Commerce.Core.Stores;

/// <summary>
/// The default <see cref="IStoreLocationService"/>: the store profile from the site's settings, and the places each
/// <see cref="IStoreLocationSource"/> adds.
/// </summary>
public sealed class DefaultStoreLocationService : IStoreLocationService
{
    private readonly ISiteService _siteService;
    private readonly IEnumerable<IStoreLocationSource> _sources;
    private readonly IAddressFormatter _addressFormatter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultStoreLocationService"/> class.
    /// </summary>
    /// <param name="siteService">The site service that holds the store profile.</param>
    /// <param name="sources">The sources of the store's other places.</param>
    /// <param name="addressFormatters">The address formatter, when the Addresses feature is on.</param>
    public DefaultStoreLocationService(
        ISiteService siteService,
        IEnumerable<IStoreLocationSource> sources,
        IEnumerable<IAddressFormatter> addressFormatters)
    {
        _siteService = siteService;
        _sources = sources;
        _addressFormatter = addressFormatters.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<StoreProfileSettings> GetProfileAsync()
    {
        var site = await _siteService.GetSiteSettingsAsync();
        var profile = site.GetOrCreate<StoreProfileSettings>() ?? new StoreProfileSettings();

        return new StoreProfileSettings
        {
            Name = string.IsNullOrWhiteSpace(profile.Name) ? site.SiteName : profile.Name,
            LogoUrl = profile.LogoUrl,
            TaxId = profile.TaxId,
            Email = profile.Email,
            Phone = profile.Phone,
            Address = profile.Address,
        };
    }

    /// <inheritdoc />
    public async Task<Address> GetAddressAsync()
        => (await _siteService.GetStoreProfileAsync()).Address;

    /// <inheritdoc />
    public async Task<string> GetAddressTextAsync()
    {
        if (await GetAddressAsync() is not { } address)
        {
            return null;
        }

        var lines = _addressFormatter is null
            ? new[] { address.AddressLine1, address.AddressLine2, string.Join(' ', new[] { address.PostalCode, address.City, address.Region }.Where(part => !string.IsNullOrWhiteSpace(part))), address.Country }
            : await _addressFormatter.GetLinesAsync(address, AddressFormatOptions.Default);

        var text = string.Join(", ", lines.Where(line => !string.IsNullOrWhiteSpace(line)).Select(line => line.Trim()));

        return text.Length == 0 ? null : text;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<NamedAddress>> ListLocationsAsync(CancellationToken cancellationToken = default)
    {
        var locations = new List<NamedAddress>();
        var profile = await GetProfileAsync();

        if (profile.Address is not null)
        {
            locations.Add(new NamedAddress { Id = StoreLocationIds.Store, Name = profile.Name, Address = profile.Address });
        }

        foreach (var source in _sources)
        {
            locations.AddRange(await source.GetLocationsAsync(cancellationToken) ?? []);
        }

        return locations;
    }
}
