using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.Services;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.Admin;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Shipping.Controllers;

/// <summary>
/// Buys a carrier's label for a shipment being packed: weigh it, compare the carriers' rates, buy one. The shipment is
/// then shipped with the label's carrier and tracking number.
/// </summary>
public sealed class ShipmentLabelsAdminController : Controller
{
    private readonly IShipmentStore _shipments;
    private readonly IShipmentService _shipmentService;
    private readonly IOrderStore _orders;
    private readonly ICarrierProviderResolver _carriers;
    private readonly ShippingOrderRateService _orderRates;
    private readonly ISiteService _siteService;
    private readonly IAuthorizationService _authorizationService;
    private readonly INotifier _notifier;
    private readonly ISession _session;

    internal readonly IHtmlLocalizer H;

    public ShipmentLabelsAdminController(
        IShipmentStore shipments,
        IShipmentService shipmentService,
        IOrderStore orders,
        ICarrierProviderResolver carriers,
        ShippingOrderRateService orderRates,
        ISiteService siteService,
        IAuthorizationService authorizationService,
        INotifier notifier,
        ISession session,
        IHtmlLocalizer<ShipmentLabelsAdminController> htmlLocalizer)
    {
        _shipments = shipments;
        _shipmentService = shipmentService;
        _orders = orders;
        _carriers = carriers;
        _orderRates = orderRates;
        _siteService = siteService;
        _authorizationService = authorizationService;
        _notifier = notifier;
        _session = session;
        H = htmlLocalizer;
    }

    [Admin("shipping/shipments/{shipmentId}/label", "ShipmentsLabel")]
    public async Task<IActionResult> Label(string shipmentId)
    {
        var (shipment, order, carrier, result) = await LoadAsync(shipmentId);

        if (result is not null)
        {
            return result;
        }

        var settings = await _siteService.GetSettingsAsync<ShippingSettings>();
        var model = new ShipmentLabelViewModel
        {
            Weight = await WeighAsync(shipment, order, settings),
            Length = settings.DefaultParcelLength,
            Width = settings.DefaultParcelWidth,
            Height = settings.DefaultParcelHeight,
        };

        return View(Populate(model, shipment, order, carrier, settings));
    }

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/label/rates", "ShipmentsLabelRates")]
    public async Task<IActionResult> Rates(string shipmentId, ShipmentLabelViewModel model)
    {
        var (shipment, order, carrier, result) = await LoadAsync(shipmentId);

        if (result is not null)
        {
            return result;
        }

        var settings = await _siteService.GetSettingsAsync<ShippingSettings>();

        Populate(model, shipment, order, carrier, settings);

        if (!model.HasShipFrom || order.ShippingAddress is null)
        {
            return View(nameof(Label), model);
        }

        if (model.Weight <= 0)
        {
            model.Error = H["Enter what the parcel weighs."].Value;

            return View(nameof(Label), model);
        }

        var quote = await carrier.QuoteAsync(new CarrierShipmentRequest
        {
            From = settings.ShipFrom,
            To = order.ShippingAddress,
            Reference = $"{order.OrderNumber}-{shipment.Number}",
            Parcel = new CarrierParcel
            {
                WeightOunces = CarrierParcel.ToOunces(model.Weight, settings.WeightUnit),
                LengthInches = CarrierParcel.ToInches(model.Length, settings.WeightUnit),
                WidthInches = CarrierParcel.ToInches(model.Width, settings.WeightUnit),
                HeightInches = CarrierParcel.ToInches(model.Height, settings.WeightUnit),
            },
        }, HttpContext.RequestAborted);

        model.Error = quote.Error;
        model.QuoteId = quote.QuoteId;
        model.Rates = [.. quote.Rates.OrderBy(rate => rate.Amount)];

        return View(nameof(Label), model);
    }

    [HttpPost]
    [Admin("shipping/shipments/{shipmentId}/label/buy", "ShipmentsLabelBuy")]
    public async Task<IActionResult> Buy(string shipmentId, ShipmentLabelViewModel model)
    {
        var (shipment, order, carrier, result) = await LoadAsync(shipmentId);

        if (result is not null)
        {
            return result;
        }

        var label = await carrier.BuyLabelAsync(model.QuoteId, model.RateId, HttpContext.RequestAborted);

        if (!label.Succeeded)
        {
            await _notifier.ErrorAsync(H["The label couldn't be bought: {0}", label.Error]);

            return RedirectToAction(nameof(Label), new { shipmentId });
        }

        shipment.LabelUrl = label.LabelUrl;
        shipment.LabelCost = label.Amount;
        shipment.LabelCurrency = label.Currency;

        // The label is paid for, so the shipment is on its way with the carrier's tracking.
        var shipped = await _shipmentService.ShipAsync(shipment, new ShipmentDetails(label.Carrier, label.TrackingNumber, label.TrackingUrl), HttpContext.RequestAborted);

        try
        {
            await _session.SaveChangesAsync(HttpContext.RequestAborted);
        }
        catch (ConcurrencyException)
        {
            await _notifier.ErrorAsync(H["The label was bought but the shipment changed at the same moment. Its tracking number is {0}; enter it on the shipment.", label.TrackingNumber]);

            return RedirectToRoute("OrdersDetail", new { orderId = order.ItemId });
        }

        await _notifier.SuccessAsync(shipped.Succeeded
            ? H["Label bought: {0}, tracking number {1}. Print it from the shipment.", label.Carrier, label.TrackingNumber]
            : H["Label bought: {0}, tracking number {1}.", label.Carrier, label.TrackingNumber]);

        return RedirectToRoute("OrdersDetail", new { orderId = order.ItemId });
    }

    private async Task<(Shipment Shipment, Orders.Core.Models.Order Order, ICarrierProvider Carrier, IActionResult Result)> LoadAsync(string shipmentId)
    {
        if (!await _authorizationService.AuthorizeAsync(User, ShippingPermissions.FulfillOrders))
        {
            return (null, null, null, Forbid());
        }

        var shipment = string.IsNullOrEmpty(shipmentId) ? null : await _shipments.FindByIdAsync(shipmentId, HttpContext.RequestAborted);
        var order = shipment is null ? null : await _orders.FindByIdAsync(shipment.OrderId, HttpContext.RequestAborted);

        if (shipment is null || order is null)
        {
            return (null, null, null, NotFound());
        }

        if (shipment.Status != ShipmentStatus.Pending)
        {
            await _notifier.WarningAsync(H["This shipment has already left."]);

            return (null, null, null, RedirectToRoute("OrdersDetail", new { orderId = order.ItemId }));
        }

        var carrier = await _carriers.GetDefaultAsync();

        if (carrier is null)
        {
            await _notifier.WarningAsync(H["Choose a default carrier provider, such as EasyPost, in Settings › Commerce › Shipping to buy labels."]);

            return (null, null, null, RedirectToRoute("OrdersDetail", new { orderId = order.ItemId }));
        }

        return (shipment, order, carrier, null);
    }

    private static ShipmentLabelViewModel Populate(ShipmentLabelViewModel model, Shipment shipment, Orders.Core.Models.Order order, ICarrierProvider carrier, ShippingSettings settings)
    {
        model.Shipment = shipment;
        model.Order = order;
        model.WeightUnit = settings.WeightUnit;
        model.HasShipFrom = settings.ShipFrom is not null;
        model.CarrierName = carrier.DisplayName;

        return model;
    }

    // The shipment's items, weighed as the checkout weighed them, plus the box.
    private async Task<decimal> WeighAsync(Shipment shipment, Orders.Core.Models.Order order, ShippingSettings settings)
    {
        var request = await _orderRates.BuildRequestAsync(order, order.ShippingAddress, HttpContext.RequestAborted);
        var weight = 0m;

        foreach (var line in shipment.Lines)
        {
            var ordered = order.Lines.FirstOrDefault(entry => entry.LineId == line.LineId);
            var requested = ordered is null ? null : request.Lines.FirstOrDefault(entry => entry.ContentItemId == ordered.ContentItemId && entry.VariantId == ordered.VariantId);

            weight += (requested?.Weight ?? 0) * line.Quantity;
        }

        return Math.Round(weight + settings.PackagingWeight, 2);
    }
}
