using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Products.Controllers;

/// <summary>
/// Compares the products a shopper picked, up to four, side by side. The comparison is the <c>ProductCompare</c>
/// shape, so a theme can override it.
/// </summary>
public sealed class ProductCompareController : Controller
{
    public const int MaxProducts = 4;

    private readonly ProductListingService _listing;
    private readonly IShapeFactory _shapeFactory;

    public ProductCompareController(ProductListingService listing, IShapeFactory shapeFactory)
    {
        _listing = listing;
        _shapeFactory = shapeFactory;
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

        IList<IShape> cards = wanted.Length == 0 ? [] : await _listing.BuildCardsAsync(wanted, withSpecifications: true);

        return View(await _shapeFactory.CreateAsync<ProductCompareViewModel>("ProductCompare", model =>
        {
            model.Cards = cards;
        }));
    }
}
