using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Fulfillment.ViewModels;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Shipping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Fulfillment.Drivers;

/// <summary>
/// The "Fulfillment partners" card on an order's admin page: what was sent to each partner and its lines, shown whenever
/// the order has requests, and a button to send or send again on a paid order. The same display type is a customer's
/// own order page, so only people who ship orders see it.
/// </summary>
internal sealed class OrderFulfillmentDisplayDriver : DisplayDriver<Order>
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthorizationService _authorizationService;
    private readonly FulfillmentRequestStore _store;
    private readonly IEnumerable<IFulfillmentProvider> _providers;

    public OrderFulfillmentDisplayDriver(
        IHttpContextAccessor httpContextAccessor,
        IAuthorizationService authorizationService,
        FulfillmentRequestStore store,
        IEnumerable<IFulfillmentProvider> providers)
    {
        _httpContextAccessor = httpContextAccessor;
        _authorizationService = authorizationService;
        _store = store;
        _providers = providers;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType != OrderConstants.DisplayTypes.DetailAdmin
            || string.IsNullOrEmpty(order.ItemId)
            || !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, ShippingPermissions.FulfillOrders))
        {
            return null;
        }

        var requests = await _store.ListByOrderAsync(order.ItemId);
        var canSend = order.Status == OrderStatus.Paid;
        var connected = new List<IFulfillmentProvider>();

        if (canSend)
        {
            foreach (var provider in _providers)
            {
                if (await provider.IsConnectedAsync())
                {
                    connected.Add(provider);
                }
            }
        }

        // Once an order has requests the card stays, read-only when the order is no longer paid.
        if (requests.Count == 0 && connected.Count == 0)
        {
            return null;
        }

        return Initialize<OrderFulfillmentViewModel>("OrderFulfillment_DetailAdmin", model =>
        {
            model.OrderId = order.ItemId;
            model.OrderNumber = order.OrderNumber;
            model.CanSend = canSend;
            model.Requests = requests
                .Select(request => new OrderFulfillmentEntry
                {
                    Request = request,
                    DisplayName = _providers.FirstOrDefault(provider => provider.Name == request.ProviderName)?.DisplayName.Value ?? request.ProviderName,
                })
                .ToList();
            model.NotSentYet = connected
                .Where(provider => !requests.Any(request => request.ProviderName == provider.Name))
                .Select(provider => provider.DisplayName.Value)
                .ToList();
        }).Location("Content:4");
    }
}
