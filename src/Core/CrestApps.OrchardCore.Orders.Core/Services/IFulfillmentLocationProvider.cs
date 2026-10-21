namespace CrestApps.OrchardCore.Orders.Core.Services;

/// <summary>
/// The places orders are fulfilled from, such as the warehouses and shops inventory is kept at, for features that let
/// an order name where it's fulfilled from, such as store pickup.
/// </summary>
public interface IFulfillmentLocationProvider
{
    Task<IReadOnlyList<FulfillmentLocation>> GetLocationsAsync();
}

public sealed record FulfillmentLocation(string LocationId, string Name);
