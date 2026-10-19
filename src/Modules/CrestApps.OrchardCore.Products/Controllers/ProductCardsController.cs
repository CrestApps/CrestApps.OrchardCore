using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.DisplayManagement;

namespace CrestApps.OrchardCore.Products.Controllers;

/// <summary>
/// The cards of a few products, as a fragment, for the shopper's recently viewed products, which their browser keeps.
/// The fragment is the <c>ProductCardStrip</c> shape, so a theme can override it.
/// </summary>
public sealed class ProductCardsController : Controller
{
    private const int MaxIds = 8;

    private readonly ProductListingService _listing;
    private readonly IShapeFactory _shapeFactory;

    public ProductCardsController(ProductListingService listing, IShapeFactory shapeFactory)
    {
        _listing = listing;
        _shapeFactory = shapeFactory;
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

        IList<IShape> cards = wanted.Length == 0 ? [] : await _listing.BuildCardsAsync(wanted);

        return PartialView(await _shapeFactory.CreateAsync<ProductCardStripViewModel>("ProductCardStrip", model =>
        {
            model.Cards = cards;
        }));
    }
}
