using CrestApps.OrchardCore.Fulfillment.Core.Services;
using CrestApps.OrchardCore.Fulfillment.Services;
using CrestApps.OrchardCore.Fulfillment.ViewModels;
using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Fulfillment.Drivers;

/// <summary>
/// The "Fulfillment partners" card on a paid order's admin page: what was sent to each partner, and a button to send
/// or retry. The same display type is a customer's own order page, so only people who manage fulfillment partners see it.
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
            || order.Status != OrderStatus.Paid
            || string.IsNullOrEmpty(order.ItemId)
            || !await _authorizationService.AuthorizeAsync(_httpContextAccessor.HttpContext?.User, FulfillmentPermissions.ManageFulfillmentPartners))
        {
            return null;
        }

        var requests = await _store.ListByOrderAsync(order.ItemId);
        var connected = new List<string>();

        foreach (var provider in _providers)
        {
            if (await provider.IsConnectedAsync())
            {
                connected.Add(provider.Name);
            }
        }

        if (requests.Count == 0 && connected.Count == 0)
        {
            return null;
        }

        return Initialize<OrderFulfillmentViewModel>("OrderFulfillment_DetailAdmin", model =>
        {
            model.OrderId = order.ItemId;
            model.OrderNumber = order.OrderNumber;
            model.Requests = requests
                .Select(request => new OrderFulfillmentEntry
                {
                    Request = request,
                    DisplayName = _providers.FirstOrDefault(provider => provider.Name == request.ProviderName)?.DisplayName.Value ?? request.ProviderName,
                })
                .ToList();
            model.NotSentYet = _providers
                .Where(provider => connected.Contains(provider.Name) && !requests.Any(request => request.ProviderName == provider.Name))
                .Select(provider => provider.DisplayName.Value)
                .ToList();
        }).Location("Content:4");
    }
}
