using CrestApps.OrchardCore.Orders.Core;
using CrestApps.OrchardCore.Orders.Core.Models;
using CrestApps.OrchardCore.Orders.Core.Services;
using CrestApps.OrchardCore.Orders.Services;
using CrestApps.OrchardCore.Orders.ViewModels;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// The returns of an order: on the buyer's order page, where they start one and follow it, and on the store's.
/// </summary>
internal sealed class OrderReturnsDisplayDriver : DisplayDriver<Order>
{
    private readonly IOrderReturnStore _returns;
    private readonly OrderReturnService _returnService;
    private readonly ISiteService _siteService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    public OrderReturnsDisplayDriver(
        IOrderReturnStore returns,
        OrderReturnService returnService,
        ISiteService siteService,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator)
    {
        _returns = returns;
        _returnService = returnService;
        _siteService = siteService;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
    }

    public override async Task<IDisplayResult> DisplayAsync(Order order, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrderConstants.DisplayTypes.Detail or OrderConstants.DisplayTypes.DetailAdmin) || order.Status != OrderStatus.Paid)
        {
            return null;
        }

        var returns = await _returns.ListByOrderAsync(order.ItemId);

        if (context.DisplayType == OrderConstants.DisplayTypes.DetailAdmin)
        {
            return returns.Count == 0
                ? null
                : Initialize<OrderReturnsViewModel>("OrderReturns_DetailAdmin", model =>
                {
                    model.Order = order;
                    model.Returns = returns;
                }).Location("Content:6");
        }

        var httpContext = _httpContextAccessor.HttpContext;
        var token = httpContext?.Request.RouteValues["token"] as string;
        var returnable = await _returnService.GetReturnableAsync(order);

        if (returns.Count == 0 && returnable.Count == 0)
        {
            return null;
        }

        var instructions = (await _siteService.GetSettingsAsync<ReturnSettings>()).Instructions;

        return Initialize<OrderReturnsViewModel>("OrderReturns_Detail", model =>
        {
            model.Order = order;
            model.Returns = returns;
            model.Instructions = instructions;
            model.RequestUrl = returnable.Count == 0
                ? null
                : string.IsNullOrEmpty(token)
                    ? _linkGenerator.GetPathByRouteValues(httpContext, "AccountOrderReturn", new { orderId = order.ItemId })
                    : _linkGenerator.GetPathByRouteValues(httpContext, "GuestOrderReturn", new { token });
            model.WithdrawUrlFormat = string.IsNullOrEmpty(token)
                ? _linkGenerator.GetPathByRouteValues(httpContext, "AccountOrderReturnWithdraw", new { orderId = order.ItemId, returnId = "__id__" })
                : _linkGenerator.GetPathByRouteValues(httpContext, "GuestOrderReturnWithdraw", new { token, returnId = "__id__" });
        }).Location("Content:6");
    }
}
