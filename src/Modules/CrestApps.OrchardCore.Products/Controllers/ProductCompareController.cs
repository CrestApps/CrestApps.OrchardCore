using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace CrestApps.OrchardCore.Products.Controllers;

/// <summary>
/// Compares the products a shopper picked, up to four, side by side.
/// </summary>
public sealed class ProductCompareController : Controller
{
    public const int MaxProducts = 4;

    private readonly ProductListingService _listing;

    public ProductCompareController(ProductListingService listing)
    {
        _listing = listing;
    }

    [HttpGet("products/compare", Name = "ProductCompare")]
    public async Task<IActionResult> Index(string ids)
    {
        var wanted = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => id.Length is > 0 and <= 26 && id.All(char.IsAsciiLetterOrDigit))
            .Distinct(StringComparer.Ordinal)
            .Take(MaxProducts)
            .ToArray();

        return View(new ProductCompareViewModel
        {
            Cards = wanted.Length == 0 ? [] : await _listing.BuildCardsAsync(wanted, withSpecifications: true),
        });
    }
}
