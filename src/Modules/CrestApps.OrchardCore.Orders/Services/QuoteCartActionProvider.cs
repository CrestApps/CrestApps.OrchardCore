using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Orders.Services;

/// <summary>
/// "Request a quote" under a signed-in buyer's Checkout button, when the store takes quote requests.
/// </summary>
public sealed class QuoteCartActionProvider : ICartActionProvider
{
    private readonly QuoteService _quotes;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public QuoteCartActionProvider(
        QuoteService quotes,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<QuoteCartActionProvider> stringLocalizer)
    {
        _quotes = quotes;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public async Task<IReadOnlyList<CartAction>> GetActionsAsync(CartPricing pricing, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || pricing?.CanCheckout != true || !await _quotes.CanRequestAsync(httpContext.User))
        {
            return [];
        }

        return
        [
            new CartAction(
                S["Request a quote"],
                _linkGenerator.GetPathByRouteValues(httpContext, "QuoteRequest", values: null),
                S["Send us your cart and we'll reply with prices, before you pay."]),
        ];
    }
}
