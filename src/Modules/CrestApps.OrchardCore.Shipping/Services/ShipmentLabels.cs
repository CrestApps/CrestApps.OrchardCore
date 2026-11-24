using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.Commerce.Stores;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Services;

/// <summary>
/// The parts of buying a carrier label that the label page of a shipment and the shipment AI tools share: the
/// ship-from address, the parcel's weight and the carrier request.
/// </summary>
internal static class ShipmentLabels
{
    /// <summary>
    /// Gets the shipping settings' ship-from address, or the store's address when it is empty.
    /// </summary>
    /// <param name="siteService">The site service.</param>
    /// <param name="settings">The shipping settings.</param>
    /// <returns>The address, or <see langword="null"/> when neither is set.</returns>
    public static async Task<Address> GetShipFromAsync(ISiteService siteService, ShippingSettings settings)
        => settings.ShipFrom ?? (await siteService.GetStoreProfileAsync()).Address;

    /// <summary>
    /// Weighs a shipment's items as the checkout weighed them, plus the box.
    /// </summary>
    /// <param name="orderRates">The service that builds an order's rate request.</param>
    /// <param name="shipment">The shipment.</param>
    /// <param name="order">Its order.</param>
    /// <param name="settings">The shipping settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The weight, in the settings' weight unit.</returns>
    public static async Task<decimal> WeighAsync(ShippingOrderRateService orderRates, Shipment shipment, Order order, ShippingSettings settings, CancellationToken cancellationToken)
    {
        var request = await orderRates.BuildRequestAsync(order, order.ShippingAddress, cancellationToken);
        var weight = 0m;

        foreach (var line in shipment.Lines)
        {
            var ordered = order.Lines.FirstOrDefault(entry => entry.LineId == line.LineId);
            var requested = ordered is null ? null : request.Lines.FirstOrDefault(entry => entry.ContentItemId == ordered.ContentItemId && entry.VariantId == ordered.VariantId);

            weight += (requested?.Weight ?? 0) * line.Quantity;
        }

        return Math.Round(weight + settings.PackagingWeight, 2);
    }

    /// <summary>
    /// Builds the request a carrier quotes a shipment's label for, with the parcel converted to ounces and inches.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="shipment">The shipment.</param>
    /// <param name="shipFrom">The ship-from address.</param>
    /// <param name="settings">The shipping settings.</param>
    /// <param name="weight">The parcel's weight, in the settings' weight unit.</param>
    /// <param name="length">The parcel's length, or <see langword="null"/>.</param>
    /// <param name="width">The parcel's width, or <see langword="null"/>.</param>
    /// <param name="height">The parcel's height, or <see langword="null"/>.</param>
    /// <returns>The request.</returns>
    public static CarrierShipmentRequest BuildRequest(Order order, Shipment shipment, Address shipFrom, ShippingSettings settings, decimal weight, decimal? length, decimal? width, decimal? height)
        => new()
        {
            From = shipFrom,
            To = order.ShippingAddress,
            Reference = $"{order.OrderNumber}-{shipment.Number}",
            Parcel = new CarrierParcel
            {
                WeightOunces = CarrierParcel.ToOunces(weight, settings.WeightUnit),
                LengthInches = CarrierParcel.ToInches(length, settings.WeightUnit),
                WidthInches = CarrierParcel.ToInches(width, settings.WeightUnit),
                HeightInches = CarrierParcel.ToInches(height, settings.WeightUnit),
            },
        };
}
