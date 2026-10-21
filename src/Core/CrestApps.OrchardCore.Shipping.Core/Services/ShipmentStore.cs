using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.Shipping.Services;

public interface IShipmentStore
{
    ValueTask<Shipment> FindByIdAsync(string shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists an order's shipments, oldest first.
    /// </summary>
    Task<IReadOnlyList<Shipment>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds the shipment sent with a tracking number, or <see langword="null"/>.
    /// </summary>
    Task<Shipment> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);

    ValueTask CreateAsync(Shipment shipment, CancellationToken cancellationToken = default);

    ValueTask UpdateAsync(Shipment shipment, CancellationToken cancellationToken = default);

    ValueTask<bool> DeleteAsync(Shipment shipment, CancellationToken cancellationToken = default);
}

public sealed class ShipmentStore : DocumentCatalog<Shipment, ShipmentIndex>, IShipmentStore
{
    public ShipmentStore(ISession session)
        : base(session)
    {
        CollectionName = ShippingConstants.CollectionName;
    }

    public async Task<Shipment> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return null;
        }

        var number = trackingNumber.Trim();

        return await Session.Query<Shipment, ShipmentIndex>(index => index.TrackingNumber == number, collection: CollectionName).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Shipment>> ListByOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(orderId))
        {
            return [];
        }

        var shipments = await Session.Query<Shipment, ShipmentIndex>(index => index.OrderId == orderId, collection: CollectionName)
            .OrderBy(index => index.CreatedUtc)
            .ThenBy(index => index.Id)
            .ListAsync(cancellationToken);

        return shipments.ToArray();
    }
}
