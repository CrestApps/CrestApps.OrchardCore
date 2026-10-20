using CrestApps.OrchardCore.Bookings.Models;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Bookings.Services;

/// <summary>
/// "Request to book" under the cart's Checkout button, for a cart holding a booking the store confirms first.
/// </summary>
internal sealed class BookingCartActionProvider : ICartActionProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public BookingCartActionProvider(IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator, IStringLocalizer<BookingCartActionProvider> stringLocalizer)
    {
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public Task<IReadOnlyList<CartAction>> GetActionsAsync(CartPricing pricing, CancellationToken cancellationToken = default)
    {
        if (_httpContextAccessor.HttpContext is not { } httpContext ||
            pricing?.CanCheckout != true ||
            !pricing.Lines.Any(line => line.ContentItem?.TryGet<BookingPart>(out var part) == true && part.RequestToBook))
        {
            return Task.FromResult<IReadOnlyList<CartAction>>([]);
        }

        return Task.FromResult<IReadOnlyList<CartAction>>(
        [
            new CartAction(
                S["Request to book"],
                _linkGenerator.GetPathByRouteValues(httpContext, "BookingRequest", values: null),
                S["The store confirms your booking first, then emails you a link to pay."]),
        ]);
    }
}
