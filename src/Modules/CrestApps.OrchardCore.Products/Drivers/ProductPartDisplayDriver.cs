using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

public sealed class ProductPartDisplayDriver : ContentPartDisplayDriver<ProductPart>
{
    internal readonly IStringLocalizer S;

    public ProductPartDisplayDriver(IStringLocalizer<ProductPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductPart part, BuildPartEditorContext context)
    {
        return Initialize<ProductPartViewModel>(GetEditorShapeType(context), model =>
        {
            model.Price = context.IsNew ? null : part.Price;
            model.Currency = string.IsNullOrEmpty(part.Currency)
                ? context.TypePartDefinition.GetSettings<ProductPartSettings>().DefaultCurrency
                : part.Currency;
            model.Sku = part.Sku;
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductPart part, UpdatePartEditorContext context)
    {
        var model = new ProductPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (!model.Price.HasValue)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price is required"]);
        }
        else if (model.Price < 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Price), S["Price cannot be negative number."]);
        }

        if (model.Price.HasValue)
        {
            part.Price = model.Price.Value;
        }

        part.Currency = string.IsNullOrWhiteSpace(model.Currency)
            ? null
            : model.Currency.Trim().ToUpperInvariant();
        part.Sku = model.Sku?.Trim();

        return Edit(part, context);
    }
}
