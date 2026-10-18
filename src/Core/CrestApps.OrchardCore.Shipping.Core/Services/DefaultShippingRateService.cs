using CrestApps.Core.Services;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Shipping.Models;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Shipping.Services;

public sealed class DefaultShippingRateService : IShippingRateService
{
    private readonly INamedCatalog<ShippingZone> _zones;
    private readonly IEnumerable<IShippingRateProvider> _providers;
    private readonly ILogger _logger;

    public DefaultShippingRateService(
        INamedCatalog<ShippingZone> zones,
        IEnumerable<IShippingRateProvider> providers,
        ILogger<DefaultShippingRateService> logger)
    {
        _zones = zones;
        _providers = providers;
        _logger = logger;
    }

    public async Task<ShippingZone> FindZoneAsync(Address destination, CancellationToken cancellationToken = default)
        => FindZone(await _zones.GetAllAsync(cancellationToken), destination);

    public async Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var zone = await FindZoneAsync(request.Destination, cancellationToken);

        if (zone is null || request.Lines.Count == 0)
        {
            return [];
        }

        var rates = new List<ShippingRate>();

        foreach (var method in zone.Methods.Where(method => method.Enabled).OrderBy(method => method.Position).ThenBy(method => method.Name, StringComparer.OrdinalIgnoreCase))
        {
            // A rate in another currency would be charged as if it were in the order's.
            if (!string.IsNullOrEmpty(method.Currency) && !string.Equals(method.Currency, request.Currency, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var provider = _providers.FirstOrDefault(provider => string.Equals(provider.Name, method.ProviderName, StringComparison.OrdinalIgnoreCase));

            if (provider is null)
            {
                _logger.LogWarning("The shipping method '{MethodId}' uses the rate provider '{ProviderName}', which isn't available.", method.MethodId, method.ProviderName);

                continue;
            }

            var surcharge = GetSurcharge(method, request);

            foreach (var rate in await provider.GetRatesAsync(new ShippingRateContext(zone, method, request), cancellationToken))
            {
                rates.Add(rate.WithAmount(Money.Round(Math.Max(0, rate.Amount + surcharge), request.Currency)));
            }
        }

        return rates;
    }

    /// <summary>
    /// Gets the first zone, in order, that covers the address, else the zone for the rest of the world.
    /// </summary>
    public static ShippingZone FindZone(IEnumerable<ShippingZone> zones, Address destination)
    {
        var ordered = (zones ?? []).OrderBy(zone => zone.Position).ThenBy(zone => zone.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        return ordered.FirstOrDefault(zone => !zone.RestOfWorld && Covers(zone, destination))
            ?? ordered.FirstOrDefault(zone => zone.RestOfWorld);
    }

    /// <summary>
    /// Gets whether the zone covers the address: its country is in the zone, whole or through the address's region.
    /// </summary>
    public static bool Covers(ShippingZone zone, Address destination)
    {
        var country = destination?.Country?.Trim();

        if (string.IsNullOrEmpty(country) || zone.Countries?.Contains(country, StringComparer.OrdinalIgnoreCase) != true)
        {
            return false;
        }

        var prefix = country + "-";
        var regions = zone.Regions?.Where(region => region.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToArray() ?? [];

        if (regions.Length == 0)
        {
            return true;
        }

        var region = destination.Region?.Trim();

        return !string.IsNullOrEmpty(region) && regions.Contains(prefix + region, StringComparer.OrdinalIgnoreCase);
    }

    // What the units in the method's surcharged shipping classes add. Classes are matched ignoring case, which a
    // dictionary read back from JSON no longer does.
    private static decimal GetSurcharge(ShippingMethod method, ShippingRateRequest request)
    {
        if (method.ClassSurcharges is not { Count: > 0 } surcharges)
        {
            return 0;
        }

        return request.Lines
            .Where(line => !string.IsNullOrEmpty(line.ShippingClass))
            .Sum(line => surcharges.FirstOrDefault(pair => string.Equals(pair.Key, line.ShippingClass, StringComparison.OrdinalIgnoreCase)).Value * line.Quantity);
    }
}
