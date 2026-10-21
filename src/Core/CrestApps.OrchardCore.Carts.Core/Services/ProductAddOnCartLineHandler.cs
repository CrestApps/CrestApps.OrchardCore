using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Core.Services;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Carts.Core.Services;

/// <summary>
/// A product's add-ons on a line: only the product's own are kept, the required ones have to be there, and each adds
/// its price to every unit, to every person a unit is for, or a share of the unit's price.
/// </summary>
internal sealed class ProductAddOnCartLineHandler : ICartLineHandler
{
    internal const int HandlerOrder = 100;

    private readonly IStringLocalizer S;

    public ProductAddOnCartLineHandler(IStringLocalizer stringLocalizer)
    {
        S = stringLocalizer;
    }

    public int Order
        => HandlerOrder;

    public Task PrepareAsync(CartLinePrepareContext context, CancellationToken cancellationToken = default)
    {
        var chosen = ProductAddOns.Normalize(context.ProductPart, context.PostedAddOns);

        if (ProductAddOns.FindMissing(context.ProductPart, chosen) is { } missing)
        {
            context.Refuse(S["Fill in {0} to add {1} to your cart.", missing.Label, context.Title]);
        }

        context.AddOns = chosen;

        return Task.CompletedTask;
    }

    public async Task EvaluateAsync(CartLineEvaluateContext context, CancellationToken cancellationToken = default)
    {
        var chosen = context.Line?.AddOns;

        if (chosen is not { Count: > 0 } || context.ProductPart?.AddOns is not { Count: > 0 } offered)
        {
            return;
        }

        // A share of the price is of the unit's price before any add-on.
        var basePrice = context.UnitPrice;
        var surcharge = 0m;

        foreach (var addOn in offered)
        {
            if (string.IsNullOrEmpty(addOn.AddOnId) || !chosen.TryGetValue(addOn.AddOnId, out var value))
            {
                continue;
            }

            var price = await GetUnitPriceAsync(addOn, basePrice, Math.Max(1, context.PeoplePerUnit), context, cancellationToken);

            surcharge += price;
            context.AddOns.Add(new ProductAddOnChoice(addOn.AddOnId, addOn.Label, addOn.Kind == ProductAddOnKind.Checkbox ? null : value, price));
        }

        context.UnitPrice += surcharge;
    }

    internal static async Task<decimal> GetUnitPriceAsync(ProductAddOn addOn, decimal basePrice, int people, CartLineEvaluateContext context, CancellationToken cancellationToken)
    {
        var price = Math.Max(0m, addOn.Price);

        return addOn.PriceBasis switch
        {
            ProductAddOnPriceBasis.PercentOfPrice => Math.Round(basePrice * Math.Min(price, 100m) / 100m, 2, MidpointRounding.AwayFromZero),
            ProductAddOnPriceBasis.PerPerson => await context.ConvertAsync(price, cancellationToken) * people,
            _ => await context.ConvertAsync(price, cancellationToken),
        };
    }
}
