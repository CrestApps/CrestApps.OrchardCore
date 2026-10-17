using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Carts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.RateLimits;

namespace CrestApps.OrchardCore.Carts.Controllers;

/// <summary>
/// The cart page and the forms that change it. Each form works without JavaScript and redirects back; a script
/// that asks for JSON gets the outcome and the new item count instead.
/// </summary>
public sealed class CartController : Controller
{
    private readonly ICartService _cartService;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;

    public CartController(
        ICartService cartService,
        IShapeFactory shapeFactory,
        INotifier notifier)
    {
        _cartService = cartService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
    }

    [HttpGet("cart", Name = CartConstants.RouteNames.Cart)]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCurrentAsync(HttpContext.RequestAborted);
        var pricing = await _cartService.PriceAsync(cart, HttpContext.RequestAborted);

        var shape = await _shapeFactory.CreateAsync<CartViewModel>("Cart", model =>
        {
            model.Pricing = pricing;
            model.CheckoutUrl = Url.RouteUrl(CartConstants.RouteNames.Checkout);
        });

        return View(shape);
    }

    [HttpPost("cart/add", Name = CartConstants.RouteNames.Add)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CartConstants.RateLimitGroup)]
    public async Task<IActionResult> Add(string contentItemId, string priceId, string variantId = null, int quantity = 1, string returnUrl = null)
        => await RespondAsync(await _cartService.AddAsync(contentItemId, priceId, variantId, quantity, HttpContext.RequestAborted), returnUrl);

    [HttpPost("cart/update", Name = CartConstants.RouteNames.Update)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CartConstants.RateLimitGroup)]
    public async Task<IActionResult> Update(string lineId, int quantity)
        => await RespondAsync(await _cartService.UpdateQuantityAsync(lineId, quantity, HttpContext.RequestAborted), returnUrl: null);

    [HttpPost("cart/remove", Name = CartConstants.RouteNames.Remove)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CartConstants.RateLimitGroup)]
    public async Task<IActionResult> Remove(string lineId)
        => await RespondAsync(await _cartService.RemoveAsync(lineId, HttpContext.RequestAborted), returnUrl: null);

    private async Task<IActionResult> RespondAsync(CartResult result, string returnUrl)
    {
        if (Request.Headers.Accept.Any(value => value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return Json(new
            {
                succeeded = result.Succeeded,
                message = result.Message?.Value,
                itemCount = result.Cart?.ItemCount ?? 0,
            });
        }

        var message = new LocalizedHtmlString(result.Message?.Name ?? string.Empty, result.Message?.Value ?? string.Empty);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(message);
        }
        else
        {
            await _notifier.ErrorAsync(message);
        }

        return !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToRoute(CartConstants.RouteNames.Cart);
    }
}
