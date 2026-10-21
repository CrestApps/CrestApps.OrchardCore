using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;

namespace CrestApps.OrchardCore.Shipping.Controllers;

/// <summary>
/// Ships an order from its admin page: a new shipment of some or all of what's left, then shipped, delivered or
/// returned. Every action goes back to the order.
/// </summary>
[Admin]
public sealed class ShipmentsAdminController : Controller
{
    private readonly IOrderStore _orders;
    private readonly IShipmentStore _shipments;
    private readonly IShipmentService _shipmentService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public ShipmentsAdminController(
        IOrderStore orders,
        IShipmentStore shipments,
        IShipmentService shipmentService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        IHtmlLocalizer<ShipmentsAdminController> htmlLocalizer)
    {
        _orders = orders;
        _shipments = shipments;
        _shipmentService = shipmentService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpPost]
    [Admin("shipping/orders/{orderId}/shipments", "ShipmentsCreate")]
    public async Task<IActionResult> Create(string orderId, [Bind(Prefix = "Shipment")] CreateShipmentViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        var order = string.IsNullOrEmpty(orderId) ? null : await _orders.FindByIdAsync(orderId);

        if (order is null)
        {
            return NotFound();
        }

        var result = await _shipmentService.CreateAsync(
            order,
            (model?.Lines ?? []).Select(line => new ShipmentLineRequest(line.LineId, line.Quantity)),
            new ShipmentDetails(model?.Carrier, model?.TrackingNumber, model?.TrackingUrl, model?.Note),
            ship: model?.Ship == true,
            HttpContext.RequestAborted);

        await NotifyAsync(result, result.Shipment?.Status == Models.ShipmentStatus.Shipped
            ? H["Shipment {0} is on its way.", result.Shipment?.Number]
            : H["Shipment {0} is ready to pack. Mark it shipped when it leaves.", result.Shipment?.Number]);

        return BackToOrder(orderId);
    }

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/ship", "ShipmentsShip")]
    public async Task<IActionResult> Ship(string shipmentId, [Bind(Prefix = "Ship")] ShipShipmentViewModel model)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        var shipment = await FindAsync(shipmentId);

        if (shipment is null)
        {
            return NotFound();
        }

        var result = await _shipmentService.ShipAsync(shipment, new ShipmentDetails(model?.Carrier, model?.TrackingNumber, model?.TrackingUrl, shipment.Note), HttpContext.RequestAborted);

        await NotifyAsync(result, H["Shipment {0} is on its way.", shipment.Number]);

        return BackToOrder(shipment.OrderId);
    }

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/deliver", "ShipmentsDeliver")]
    public async Task<IActionResult> Deliver(string shipmentId)
        => await ChangeAsync(shipmentId, (shipment, ct) => _shipmentService.DeliverAsync(shipment, ct), number => H["Shipment {0} was delivered.", number]);

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/return", "ShipmentsReturn")]
    public async Task<IActionResult> Return(string shipmentId)
        => await ChangeAsync(shipmentId, (shipment, ct) => _shipmentService.ReturnAsync(shipment, ct), number => H["Shipment {0} came back. Its items can be shipped again.", number]);

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/delete", "ShipmentsDelete")]
    public async Task<IActionResult> Delete(string shipmentId)
        => await ChangeAsync(shipmentId, (shipment, ct) => _shipmentService.DeleteAsync(shipment, ct), number => H["Shipment {0} was removed, and its items put back.", number]);

    private async Task<IActionResult> ChangeAsync(string shipmentId, Func<Models.Shipment, CancellationToken, Task<ShipmentResult>> change, Func<int, LocalizedHtmlString> success)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return Forbid();
        }

        var shipment = await FindAsync(shipmentId);

        if (shipment is null)
        {
            return NotFound();
        }

        var result = await change(shipment, HttpContext.RequestAborted);

        await NotifyAsync(result, success(shipment.Number));

        return BackToOrder(shipment.OrderId);
    }

    private async Task<Models.Shipment> FindAsync(string shipmentId)
        => string.IsNullOrEmpty(shipmentId) ? null : await _shipments.FindByIdAsync(shipmentId, HttpContext.RequestAborted);

    private async Task NotifyAsync(ShipmentResult result, LocalizedHtmlString success)
    {
        switch (result.Error)
        {
            case ShipmentError.None:
                await _notifier.SuccessAsync(success);
                break;
            case ShipmentError.OrderNotPaid:
                await _notifier.ErrorAsync(H["Only a paid order can be shipped."]);
                break;
            case ShipmentError.NothingToShip:
                await _notifier.ErrorAsync(H["Enter how many of each item go in the shipment."]);
                break;
            case ShipmentError.TooMany:
                await _notifier.ErrorAsync(H["That's more than is left to ship. Lower the quantities."]);
                break;
            default:
                await _notifier.ErrorAsync(H["The shipment can't change that way now. Reload the order and try again."]);
                break;
        }
    }

    private RedirectToRouteResult BackToOrder(string orderId)
        => RedirectToRoute("OrdersDetail", new { area = "CrestApps.OrchardCore.Orders", orderId });
}
