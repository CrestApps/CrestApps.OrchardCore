using CrestApps.Core;
using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.Services;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Edits a product's variants: the values of each option its type defines, and the SKU, prices, image and switch
/// of every combination. Saving after changing the values adds the new combinations and drops the ones gone.
/// </summary>
public sealed class ProductVariantsPartDisplayDriver : ContentPartDisplayDriver<ProductVariantsPart>
{
    private static readonly char[] _separators = [',', ';', '\n'];

    internal readonly IStringLocalizer S;

    public ProductVariantsPartDisplayDriver(IStringLocalizer<ProductVariantsPartDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ProductVariantsPart part, BuildPartEditorContext context)
    {
        return Initialize<ProductVariantsPartViewModel>(GetEditorShapeType(context), model =>
        {
            var names = GetOptionNames(context.TypePartDefinition, part);

            model.Options = names
                .Select(name => new ProductOptionViewModel
                {
                    Name = name,
                    Values = string.Join(", ", part.Options?.FirstOrDefault(option => string.Equals(option.Name, name, StringComparison.OrdinalIgnoreCase))?.Values ?? []),
                })
                .ToList();

            model.Variants = (part.Variants ?? [])
                .Select(variant => new ProductVariantViewModel
                {
                    VariantId = variant.VariantId,
                    Title = variant.GetTitle(names),
                    Sku = variant.Sku,
                    Price = variant.Price,
                    CompareAtPrice = variant.CompareAtPrice,
                    SalePrice = variant.SalePrice,
                    ImagePath = variant.ImagePath,
                    Enabled = variant.Enabled,
                })
                .ToList();

            if (part.ContentItem.TryGet<ProductPart>(out var product))
            {
                model.ProductSku = product.Sku;
                model.ProductPrice = product.Price;
            }

            // A variant shows one of the product's own photos, so its page can switch to it.
            model.Images = ProductGalleryReader.Find(context.TypePartDefinition?.ContentTypeDefinition, part.ContentItem)?.Paths.ToList() ?? [];
        });
    }

    public override async Task<IDisplayResult> UpdateAsync(ProductVariantsPart part, UpdatePartEditorContext context)
    {
        var model = new ProductVariantsPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var names = GetOptionNames(context.TypePartDefinition, part);

        // What was typed into each variant row is kept on the variant first, so a change of values that keeps its
        // combination keeps the SKU and prices just entered.
        var posted = (model.Variants ?? []).Where(variant => !string.IsNullOrEmpty(variant?.VariantId)).ToDictionary(variant => variant.VariantId, StringComparer.Ordinal);
        var index = -1;

        foreach (var variant in part.Variants ?? [])
        {
            index++;

            if (!posted.TryGetValue(variant.VariantId ?? string.Empty, out var entry))
            {
                continue;
            }

            if (entry.Price < 0m)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Variants)}[{index}].{nameof(entry.Price)}", S["Enter a price of zero or more."]);
            }

            if (entry.CompareAtPrice < 0m)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Variants)}[{index}].{nameof(entry.CompareAtPrice)}", S["Enter a price of zero or more."]);
            }

            variant.Sku = entry.Sku?.Trim();
            variant.Price = entry.Price;
            if (entry.SalePrice < 0m)
            {
                context.Updater.ModelState.AddModelError(Prefix, $"{nameof(model.Variants)}[{index}].{nameof(entry.SalePrice)}", S["Enter a price of zero or more."]);
            }

            variant.CompareAtPrice = entry.CompareAtPrice;
            variant.SalePrice = entry.SalePrice;
            variant.ImagePath = string.IsNullOrWhiteSpace(entry.ImagePath) ? null : entry.ImagePath.Trim();
            variant.Enabled = entry.Enabled;
        }

        part.Options = names
            .Select(name => new ProductOption
            {
                Name = name,
                Values = (model.Options?.FirstOrDefault(option => string.Equals(option?.Name, name, StringComparison.OrdinalIgnoreCase))?.Values ?? string.Empty)
                    .Split(_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList(),
            })
            .Where(option => option.Values.Count > 0)
            .ToList();

        part.Variants = ProductVariants.Combine(part.Options, part.Variants, UniqueId.GenerateId);

        var skus = part.Variants.Where(variant => !string.IsNullOrEmpty(variant.Sku)).GroupBy(variant => variant.Sku, StringComparer.OrdinalIgnoreCase);

        foreach (var duplicate in skus.Where(group => group.Count() > 1))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Variants), S["The SKU '{0}' is used by more than one variant.", duplicate.Key]);
        }

        return Edit(part, context);
    }

    // The type's options, in their order. Options a product still has values for, but the type no longer lists,
    // are dropped on the next save.
    private static string[] GetOptionNames(ContentTypePartDefinition typePartDefinition, ProductVariantsPart part)
    {
        var settings = typePartDefinition?.GetSettings<ProductVariantsPartSettings>();

        return (settings?.Options ?? [])
            .Select(option => option?.Name?.Trim())
            .Where(name => !string.IsNullOrEmpty(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
