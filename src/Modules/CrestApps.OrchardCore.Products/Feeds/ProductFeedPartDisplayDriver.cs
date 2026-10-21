using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// The Product Feed part's editor: brand, GTIN, MPN, condition and Google category, or staying out of feeds.
/// </summary>
internal sealed class ProductFeedPartDisplayDriver : ContentPartDisplayDriver<ProductFeedPart>
{
    internal readonly IStringLocalizer S;

    public ProductFeedPartDisplayDriver(IStringLocalizer<ProductFeedPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductFeedPart part, BuildPartEditorContext context)
        => Initialize<ProductFeedPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Brand = part.Brand;
            model.Gtin = part.Gtin;
            model.Mpn = part.Mpn;
            model.Condition = part.Condition;
            model.GoogleProductCategory = part.GoogleProductCategory;
            model.ExcludeFromFeeds = part.ExcludeFromFeeds;
        });

    public override async Task<IDisplayResult> UpdateAsync(ProductFeedPart part, UpdatePartEditorContext context)
    {
        var model = new ProductFeedPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!string.IsNullOrWhiteSpace(model.Gtin) && !ProductFeedValidator.IsValidGtin(model.Gtin))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Gtin), S["Enter a GTIN of 8, 12, 13 or 14 digits with its check digit, such as the barcode's number."]);
        }

        part.Brand = Clean(model.Brand, 70);
        part.Gtin = Clean(model.Gtin, 14);
        part.Mpn = Clean(model.Mpn, 70);
        part.Condition = model.Condition is "new" or "refurbished" or "used" ? model.Condition : null;
        part.GoogleProductCategory = Clean(model.GoogleProductCategory, 250);
        part.ExcludeFromFeeds = model.ExcludeFromFeeds;

        return Edit(part, context);
    }

    private static string Clean(string value, int length)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, length)];
}
