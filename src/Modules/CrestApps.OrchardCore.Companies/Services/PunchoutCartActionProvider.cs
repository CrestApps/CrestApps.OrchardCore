using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Companies.Services;

/// <summary>
/// "Transfer to your procurement system" on the cart of a buyer who came in by punchout.
/// </summary>
public sealed class PunchoutCartActionProvider : ICartActionProvider
{
    private readonly PunchoutService _punchout;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly LinkGenerator _linkGenerator;

    internal readonly IStringLocalizer S;

    public PunchoutCartActionProvider(
        PunchoutService punchout,
        IHttpContextAccessor httpContextAccessor,
        LinkGenerator linkGenerator,
        IStringLocalizer<PunchoutCartActionProvider> stringLocalizer)
    {
        _punchout = punchout;
        _httpContextAccessor = httpContextAccessor;
        _linkGenerator = linkGenerator;
        S = stringLocalizer;
    }

    public Task<IReadOnlyList<CartAction>> GetActionsAsync(CartPricing pricing, CancellationToken cancellationToken = default)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || pricing is not { Lines.Count: > 0 } || _punchout.GetCurrent() is null)
        {
            return Task.FromResult<IReadOnlyList<CartAction>>([]);
        }

        return Task.FromResult<IReadOnlyList<CartAction>>(
        [
            new CartAction(
                S["Transfer to your procurement system"],
                _linkGenerator.GetPathByRouteValues(httpContext, "PunchoutTransfer", values: null),
                S["Sends this cart back to your purchasing system, where the order is approved and placed."]),
        ]);
    }
}
