using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Products.Controllers;

/// <summary>
/// The cards of a few products, as a fragment, for the shopper's recently viewed products, which their browser keeps.
/// </summary>
public sealed class ProductCardsController : Controller
{
    private const int MaxIds = 8;

    private readonly ProductListingService _listing;

    public ProductCardsController(ProductListingService listing)
    {
        _listing = listing;
    }

    [HttpGet("products/cards", Name = "ProductCards")]
    public async Task<IActionResult> Index(string ids)
    {
        var wanted = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => id.Length is > 0 and <= 26 && id.All(char.IsAsciiLetterOrDigit))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxIds)
            .ToArray();

        return PartialView("ProductCardStripCards", new ProductCardStripViewModel
        {
            Cards = wanted.Length == 0 ? [] : await _listing.BuildCardsAsync(wanted),
        });
    }
}
