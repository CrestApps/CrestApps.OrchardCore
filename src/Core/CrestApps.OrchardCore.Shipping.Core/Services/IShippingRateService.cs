using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Shipping.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Finds the zone an address is in, and what its methods charge to ship there.
/// </summary>
public interface IShippingRateService
{
    /// <summary>
    /// Gets the first zone, in order, that covers the address, else the zone for the rest of the world, else
    /// <see langword="null"/>.
    /// </summary>
    Task<ShippingZone> FindZoneAsync(Address destination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the rates of the enabled methods of the destination's zone, in the zone's order. Methods in another
    /// currency, methods whose provider is missing, and methods that don't apply are left out.
    /// </summary>
    Task<IReadOnlyList<ShippingRate>> GetRatesAsync(ShippingRateRequest request, CancellationToken cancellationToken = default);
}
