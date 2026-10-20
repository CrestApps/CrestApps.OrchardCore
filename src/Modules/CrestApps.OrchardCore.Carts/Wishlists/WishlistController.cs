using System.Security.Claims;
using CrestApps.OrchardCore.Products.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display;
using OrchardCore.DisplayManagement;
using OrchardCore.DisplayManagement.ModelBinding;
using OrchardCore.DisplayManagement.Notify;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Carts.Wishlists;

/// <summary>
/// The wishlist: saving a product from its page, and the account page that lists what was saved.
/// </summary>
[Feature(Core.CartConstants.Features.Wishlists)]
[Authorize]
public sealed class WishlistController : Controller
{
    private readonly WishlistService _wishlists;
    private readonly IContentManager _contentManager;
    private readonly IContentItemDisplayManager _displayManager;
    private readonly IUpdateModelAccessor _updateModelAccessor;
    private readonly IShapeFactory _shapeFactory;
    private readonly INotifier _notifier;

    internal readonly IHtmlLocalizer H;

    public WishlistController(
        WishlistService wishlists,
        IContentManager contentManager,
        IContentItemDisplayManager displayManager,
        IUpdateModelAccessor updateModelAccessor,
        IShapeFactory shapeFactory,
        INotifier notifier,
        IHtmlLocalizer<WishlistController> htmlLocalizer)
    {
        _wishlists = wishlists;
        _contentManager = contentManager;
        _displayManager = displayManager;
        _updateModelAccessor = updateModelAccessor;
        _shapeFactory = shapeFactory;
        _notifier = notifier;
        H = htmlLocalizer;
    }

    [HttpGet("account/wishlist", Name = "AccountWishlist")]
    public async Task<IActionResult> Index()
    {
        var wishlist = await _wishlists.GetAsync(UserId, HttpContext.RequestAborted);
        var ids = wishlist?.Items.Select(item => item.ContentItemId).ToList() ?? [];
        var products = (await _contentManager.GetAsync(ids, VersionOptions.Published))
            .Where(item => item.Has<ProductPart>())
            .ToDictionary(item => item.ContentItemId, StringComparer.Ordinal);

        var entries = new List<WishlistEntry>();

        // In the order they were saved, newest first; a product no longer sold drops off the page.
        foreach (var item in wishlist?.Items ?? [])
        {
            if (products.TryGetValue(item.ContentItemId, out var product))
            {
                entries.Add(new WishlistEntry
                {
                    Product = product,
                    AddedUtc = item.AddedUtc,
                    Actions = await _displayManager.BuildDisplayAsync(product, _updateModelAccessor.ModelUpdater, "ProductAction"),
                });
            }
        }

        return View(await _shapeFactory.CreateAsync<WishlistViewModel>("AccountWishlist", model => model.Entries = entries));
    }

    [HttpPost("wishlist/{contentItemId}/add", Name = "WishlistAdd")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(string contentItemId, string returnUrl)
    {
        if (await _contentManager.GetAsync(contentItemId, VersionOptions.Published) is not { } product || !product.Has<ProductPart>())
        {
            return NotFound();
        }

        await _wishlists.AddAsync(UserId, contentItemId, HttpContext.RequestAborted);
        await _notifier.SuccessAsync(H["{0} is saved to your wishlist.", product.DisplayText]);

        return Back(returnUrl);
    }

    [HttpPost("wishlist/{contentItemId}/remove", Name = "WishlistRemove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(string contentItemId, string returnUrl)
    {
        await _wishlists.RemoveAsync(UserId, contentItemId, HttpContext.RequestAborted);

        return Back(returnUrl);
    }

    private string UserId
        => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private IActionResult Back(string returnUrl)
        => Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToRoute("AccountWishlist");
}

public class WishlistViewModel
{
    public IReadOnlyList<WishlistEntry> Entries { get; set; } = [];
}

public class WishlistEntry
{
    public ContentItem Product { get; set; }

    public DateTime AddedUtc { get; set; }

    /// <summary>
    /// Gets or sets the product's add to cart, with its variants and options, as on a listing.
    /// </summary>
    public IShape Actions { get; set; }
}

public class WishlistButtonViewModel
{
    public string ContentItemId { get; set; }

    public bool IsSignedIn { get; set; }

    public bool IsSaved { get; set; }

    public string ReturnUrl { get; set; }
}
