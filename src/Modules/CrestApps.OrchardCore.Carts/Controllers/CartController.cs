using CrestApps.OrchardCore.Carts.Core;
using CrestApps.OrchardCore.Carts.Core.Models;
using CrestApps.OrchardCore.Carts.Core.Services;
using CrestApps.OrchardCore.Carts.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
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

    internal readonly IHtmlLocalizer H;
    internal readonly IStringLocalizer S;

    public CartController(
        ICartService cartService,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IHtmlLocalizer<CartController> htmlLocalizer,
        IStringLocalizer<CartController> stringLocalizer)
    {
        S = stringLocalizer;
        _cartService = cartService;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("cart", Name = CartConstants.RouteNames.Cart)]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCurrentAsync(HttpContext.RequestAborted);
        var pricing = await _cartService.PriceAsync(cart, HttpContext.RequestAborted);
        var actions = new List<CartAction>();

        foreach (var provider in HttpContext.RequestServices.GetServices<ICartActionProvider>())
        {
            actions.AddRange(await provider.GetActionsAsync(pricing, HttpContext.RequestAborted));
        }

        var shape = await _shapeFactory.CreateAsync<CartViewModel>("Cart", model =>
        {
            model.Pricing = pricing;
            model.CheckoutUrl = Url.RouteUrl(CartConstants.RouteNames.Checkout);
            model.Actions = actions;
        });

        return View(shape);
    }

    /// <summary>
    /// The cart as a fragment for the slide-out mini cart: its lines, subtotal and the way to the cart and checkout.
    /// </summary>
    [HttpGet("cart/mini", Name = "CartMini")]
    public async Task<IActionResult> Mini()
    {
        var cart = await _cartService.GetCurrentAsync(HttpContext.RequestAborted);
        var pricing = await _cartService.PriceAsync(cart, HttpContext.RequestAborted);

        // The fragment is the MiniCart shape, so a theme changes it by overriding that shape.
        var shape = await _shapeFactory.CreateAsync<CartViewModel>("MiniCart", model =>
        {
            model.Pricing = pricing;
            model.CheckoutUrl = Url.RouteUrl(CartConstants.RouteNames.Checkout);
        });

        return PartialView(shape);
    }

    [HttpPost("cart/add", Name = CartConstants.RouteNames.Add)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CartConstants.RateLimitGroup)]
    public async Task<IActionResult> Add(string contentItemId, string priceId, string variantId = null, int quantity = 1, string returnUrl = null, Dictionary<string, string> addOns = null)
        => await RespondAsync(await _cartService.AddAsync(contentItemId, priceId, variantId, quantity, addOns, HttpContext.RequestAborted), returnUrl, linkToCart: true);

    [HttpPost("cart/add-many", Name = CartConstants.RouteNames.AddMany)]
    [ValidateAntiForgeryToken]
    [RateLimitGroup(CartConstants.RateLimitGroup)]
    public async Task<IActionResult> AddMany(List<CartAddManyItem> items, string returnUrl = null)
    {
        var chosen = (items ?? [])
            .Where(item => item is not null && item.Quantity > 0 && !string.IsNullOrEmpty(item.ContentItemId))
            .Take(Products.Core.Models.ProductGroupPart.MaxProducts)
            .ToList();

        if (chosen.Count == 0)
        {
            return await RespondAsync(CartResult.Failure(S["Enter how many of each product you want."], await _cartService.GetCurrentAsync(HttpContext.RequestAborted)), returnUrl);
        }

        // Each product is added on its own, so one that can't be bought doesn't stop the others.
        CartResult last = null;
        var added = 0;
        var problems = new List<string>();

        foreach (var item in chosen)
        {
            last = await _cartService.AddAsync(item.ContentItemId, null, null, item.Quantity, HttpContext.RequestAborted);

            if (last.Succeeded)
            {
                added++;
            }
            else if (last.Message is { } problem)
            {
                problems.Add(problem.Value);
            }
        }

        var message = problems.Count == 0
            ? added == 1 ? last.Message : S["{0} products were added to your cart.", added]
            : added == 0
                ? new LocalizedString("problems", string.Join(" ", problems))
                : S["Added {0} to your cart. {1}", S.Plural(added, "1 product", "{0} products"), string.Join(" ", problems)];

        return await RespondAsync(added > 0 ? CartResult.Success(last.Cart, message) : CartResult.Failure(message, last?.Cart), returnUrl, linkToCart: added > 0);
    }

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

    private async Task<IActionResult> RespondAsync(CartResult result, string returnUrl, bool linkToCart = false)
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

        var redirectsToCart = string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl);

        // The message names a product, so it is encoded. Themes without a cart link in their header still let the
        // shopper reach the cart from the notice.
        var message = result.Succeeded && linkToCart && !redirectsToCart
            ? H["{0} <a href=\"{1}\" class=\"alert-link text-reset text-decoration-underline\">View cart</a>", result.Message?.Value ?? string.Empty, Url.RouteUrl(CartConstants.RouteNames.Cart)]
            : new LocalizedHtmlString(result.Message?.Name ?? string.Empty, "{0}", false, result.Message?.Value ?? string.Empty);

        if (result.Succeeded)
        {
            await _notifier.SuccessAsync(message);
        }
        else
        {
            await _notifier.ErrorAsync(message);
        }

        return redirectsToCart
            ? RedirectToRoute(CartConstants.RouteNames.Cart)
            : LocalRedirect(returnUrl);
    }
}

/// <summary>
/// A product and how many of it to add, in a form that adds several at once.
/// </summary>
public sealed class CartAddManyItem
{
    public string ContentItemId { get; set; }

    public int Quantity { get; set; }
}
