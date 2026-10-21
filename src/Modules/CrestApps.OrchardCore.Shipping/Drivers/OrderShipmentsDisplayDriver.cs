using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// The shipments of an order with goods: on its admin page, where they are made and updated, and on the buyer's
/// order pages, with their tracking links.
/// </summary>
internal sealed class OrderShipmentsDisplayDriver : DisplayDriver<Order>
{
    private readonly IShipmentStore _shipments;
    private readonly IShipmentService _shipmentService;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICarrierProviderResolver _carriers;

    public OrderShipmentsDisplayDriver(
        IShipmentStore shipments,
        IShipmentService shipmentService,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        ICarrierProviderResolver carriers)
    {
        _carriers = carriers;
        _shipments = shipments;
        _shipmentService = shipmentService;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrderConstants.DisplayTypes.DetailAdmin or OrderConstants.DisplayTypes.Detail) ||
            order.Status != OrderStatus.Paid ||
            !ShippingOrderRateService.Ships(order))
        {
            return null;
        }

        var shipments = await _shipments.ListByOrderAsync(order.ItemId);

        if (context.DisplayType == OrderConstants.DisplayTypes.Detail)
        {
            return shipments.Any(shipment => shipment.Status != ShipmentStatus.Pending)
                ? Initialize<OrderShipmentsViewModel>("OrderShipments_Detail", model =>
                {
                    model.Order = order;
                    model.Shipments = [.. shipments.Where(shipment => shipment.Status != ShipmentStatus.Pending)];
                }).Location("Content:5")
                : null;
        }

        var canFulfill = await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.FulfillOrders);

        // A buyer looking at their own order under My Orders sees what left, not the packing.
        if (!canFulfill && !shipments.Any(shipment => shipment.Status != ShipmentStatus.Pending))
        {
            return null;
        }

        var remaining = await _shipmentService.GetRemainingAsync(order);

        return Initialize<OrderShipmentsViewModel>(canFulfill ? "OrderShipments_DetailAdmin" : "OrderShipments_Detail", async model =>
        {
            model.Order = order;
            model.Shipments = canFulfill ? shipments : [.. shipments.Where(shipment => shipment.Status != ShipmentStatus.Pending)];
            model.CanFulfill = canFulfill;
            model.Carriers = [.. ((await _siteService.GetSettingsAsync<ShippingSettings>()).Carriers ?? []).Select(carrier => carrier.Name)];

            model.CanBuyLabels = canFulfill && await _carriers.GetDefaultAsync() is not null;
            model.Remaining = order.Lines
                .Where(line => remaining.ContainsKey(line.LineId))
                .Select(line => new ShipmentLineChoice
                {
                    LineId = line.LineId,
                    Title = OrderLineText.DescribeForPacking(line),
                    Sku = line.Sku,
                    Remaining = remaining[line.LineId],
                })
                .ToArray();
        }).Location("Content:5");
    }
}
