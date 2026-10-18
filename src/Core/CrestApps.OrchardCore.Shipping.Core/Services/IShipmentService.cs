using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// Ships orders, in one or several shipments, and keeps each order's fulfillment status. Changes are made in the
/// caller's unit of work.
/// </summary>
public interface IShipmentService
{
    /// <summary>
    /// Gets how many units of each of the order's goods (by line id) aren't in a shipment yet, leaving out lines with
    /// nothing left. Units in a returned shipment count as not shipped.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> GetRemainingAsync(Order order, CancellationToken cancellationToken = default);

    /// <summary>
    /// Puts units of the order's goods in a new shipment, pending or, with <paramref name="ship"/>, shipped now.
    /// </summary>
    Task<ShipmentResult> CreateAsync(Order order, IEnumerable<ShipmentLineRequest> lines, ShipmentDetails details, bool ship, CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a pending shipment shipped, with its carrier and tracking number.
    /// </summary>
    Task<ShipmentResult> ShipAsync(Shipment shipment, ShipmentDetails details, CancellationToken cancellationToken = default);

    Task<ShipmentResult> DeliverAsync(Shipment shipment, CancellationToken cancellationToken = default);

    Task<ShipmentResult> ReturnAsync(Shipment shipment, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a shipment that hasn't left, putting its units back.
    /// </summary>
    Task<ShipmentResult> DeleteAsync(Shipment shipment, CancellationToken cancellationToken = default);
}

public sealed record ShipmentLineRequest(string LineId, int Quantity);

/// <summary>
/// How a shipment travels. A tracking link left empty is made from the carrier's link in the settings.
/// </summary>
public sealed record ShipmentDetails(string Carrier, string TrackingNumber, string TrackingUrl = null, string Note = null);

public sealed class ShipmentResult
{
    public bool Succeeded
        => Error == ShipmentError.None;

    public ShipmentError Error { get; init; }

    public Shipment Shipment { get; init; }

    public static ShipmentResult Success(Shipment shipment)
        => new() { Shipment = shipment };

    public static ShipmentResult Fail(ShipmentError error)
        => new() { Error = error };
}

public enum ShipmentError
{
    None = 0,

    /// <summary>
    /// Only a paid order is shipped.
    /// </summary>
    OrderNotPaid = 1,

    /// <summary>
    /// The shipment would hold nothing.
    /// </summary>
    NothingToShip = 2,

    /// <summary>
    /// More units than the order has left to ship.
    /// </summary>
    TooMany = 3,

    /// <summary>
    /// The shipment can't change that way from where it is, such as delivering one that hasn't left.
    /// </summary>
    InvalidStatus = 4,
}
