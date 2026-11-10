using System.Text.Json.Nodes;
using CrestApps.OrchardCore.AgenticCommerce.Core.Services;
using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AgenticCommerce.AI;

/// <summary>
/// Writes the current visitor's storefront cart for the cart AI tools.
/// </summary>
internal static class StoreCartJson
{
    /// <summary>
    /// Writes a cart, priced now, with the link to the store's cart page.
    /// </summary>
    /// <param name="services">The request services.</param>
    /// <param name="httpContext">The visitor's request.</param>
    /// <param name="cart">The visitor's cart, or <see langword="null"/> when they have none.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The cart.</returns>
    public static async Task<JsonObject> WriteAsync(IServiceProvider services, HttpContext httpContext, Cart cart, CancellationToken cancellationToken)
    {
        var json = new JsonObject();
        var items = new JsonArray();

        if (cart is { Lines.Count: > 0 })
        {
            var pricing = await services.GetRequiredService<ICartService>().PriceAsync(cart, cancellationToken);
            var resolver = services.GetRequiredService<IAgentItemResolver>();

            foreach (var line in pricing.Lines)
            {
                var item = new JsonObject
                {
                    ["id"] = resolver.GetItemId(line.Line.ContentItemId, line.Line.VariantId, line.Product?.Sku),
                    ["title"] = line.Title,
                    ["quantity"] = line.Line.Quantity,
                    ["unit_price"] = line.UnitPrice,
                    ["total"] = line.LineTotal,
                };

                if (line.Problem is { } problem)
                {
                    item["problem"] = problem.Value;
                }

                items.Add(item);
            }

            json["currency"] = pricing.Currency;
            json["subtotal"] = pricing.Subtotal;
            json["item_count"] = pricing.ItemCount;
            json["can_checkout"] = pricing.CanCheckout;
        }
        else
        {
            json["item_count"] = 0;
            json["can_checkout"] = false;
        }

        json["items"] = items;

        var cartUrl = services.GetService<LinkGenerator>()?.GetUriByRouteValues(httpContext, CartConstants.RouteNames.Cart, values: null);

        if (!string.IsNullOrEmpty(cartUrl))
        {
            json["cart_url"] = cartUrl;
        }

        return json;
    }
}
