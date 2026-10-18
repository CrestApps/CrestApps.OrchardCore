using CrestApps.OrchardCore.Checkout;
using CrestApps.OrchardCore.Checkout.Services;
using CrestApps.OrchardCore.Orders.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// Names an order a checkout paid for by its number, linked to its admin page.
/// </summary>
public sealed class OrderCheckoutReferenceDescriber : ICheckoutReferenceDescriber
{
    private readonly IOrderStore _orderStore;
    private readonly LinkGenerator _linkGenerator;
    private readonly IHttpContextAccessor _httpContextAccessor;

    internal readonly IStringLocalizer S;

    public OrderCheckoutReferenceDescriber(
        IOrderStore orderStore,
        LinkGenerator linkGenerator,
        IHttpContextAccessor httpContextAccessor,
        IStringLocalizer<OrderCheckoutReferenceDescriber> stringLocalizer)
    {
        _orderStore = orderStore;
        _linkGenerator = linkGenerator;
        _httpContextAccessor = httpContextAccessor;
        S = stringLocalizer;
    }

    public async Task<CheckoutReferenceDescription> DescribeAsync(string referenceType, string referenceId, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(referenceType, CheckoutReferenceTypes.Order, StringComparison.Ordinal) || string.IsNullOrEmpty(referenceId))
        {
            return null;
        }

        var order = await _orderStore.FindByIdAsync(referenceId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        return new CheckoutReferenceDescription
        {
            Text = S["Order {0}", order.OrderNumber],
            Url = httpContext is null ? null : _linkGenerator.GetPathByName(httpContext, "OrdersDetail", new { orderId = order.ItemId }),
        };
    }
}
