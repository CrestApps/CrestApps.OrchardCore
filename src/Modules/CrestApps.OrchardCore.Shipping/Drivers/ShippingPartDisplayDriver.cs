using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Shipping.Models;
using CrestApps.OrchardCore.Shipping.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Display.ContentDisplay;
using OrchardCore.ContentManagement.Display.Models;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Shipping.Drivers;

/// <summary>
/// A product's weight and size, the weight and size of variants that differ, and its shipping class.
/// </summary>
public sealed class ShippingPartDisplayDriver : ContentPartDisplayDriver<ShippingPart>
{
    private const decimal MaxWeight = 1_000_000;

    private const decimal MaxSize = 100_000;

    private readonly ISiteService _siteService;

    internal readonly IStringLocalizer S;

    public ShippingPartDisplayDriver(ISiteService siteService, IStringLocalizer<ShippingPartDisplayDriver> stringLocalizer)
    {
        _siteService = siteService;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ShippingPart part, BuildPartEditorContext context)
        => Initialize<ShippingPartViewModel>(GetEditorShapeType(context), async model =>
        {
            var settings = await _siteService.GetSettingsAsync<ShippingSettings>();

            model.Weight = part.Weight;
            model.Length = part.Length;
            model.Width = part.Width;
            model.Height = part.Height;
            model.ShippingClass = part.ShippingClass;
            model.WeightUnit = settings.WeightUnit;
            model.ShippingClasses = settings.ShippingClasses ?? [];
            model.Variants = GetVariants(part)
                .Select(variant => new ShippingVariantWeightViewModel
                {
                    VariantId = variant.VariantId,
                    Title = variant.Title,
                    Weight = part.VariantWeights?.TryGetValue(variant.VariantId, out var weight) == true ? weight : null,
                    Length = Dimensions(part, variant.VariantId)?.Length,
                    Width = Dimensions(part, variant.VariantId)?.Width,
                    Height = Dimensions(part, variant.VariantId)?.Height,
                })
                .ToList();
        });

    public override async Task<IDisplayResult> UpdateAsync(ShippingPart part, UpdatePartEditorContext context)
    {
        var model = new ShippingPartViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (model.Weight is < 0 or > MaxWeight)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Weight), S["Enter a weight from 0 to {0}.", MaxWeight]);
        }

        if (!IsSize(model.Length) || !IsSize(model.Width) || !IsSize(model.Height))
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(model.Length), S["Enter a length, width and height from 0 to {0}.", MaxSize]);
        }

        part.Weight = model.Weight;
        part.Length = model.Length;
        part.Width = model.Width;
        part.Height = model.Height;
        part.ShippingClass = string.IsNullOrWhiteSpace(model.ShippingClass) ? null : model.ShippingClass.Trim();

        // Only the variants the product has now are kept, and only when they weigh something of their own.
        var variantIds = GetVariants(part).Select(variant => variant.VariantId).ToHashSet(StringComparer.Ordinal);

        part.VariantWeights = (model.Variants ?? [])
            .Where(variant => variant.Weight is >= 0 and <= MaxWeight && variantIds.Contains(variant.VariantId ?? string.Empty))
            .ToDictionary(variant => variant.VariantId, variant => variant.Weight.Value, StringComparer.Ordinal);

        // A variant's own size counts only with its length, width and height all entered.
        part.VariantDimensions = (model.Variants ?? [])
            .Where(variant => variant.Length is > 0 and <= MaxSize && variant.Width is > 0 and <= MaxSize && variant.Height is > 0 and <= MaxSize &&
                variantIds.Contains(variant.VariantId ?? string.Empty))
            .ToDictionary(
                variant => variant.VariantId,
                variant => new ShippingDimensions
                {
                    Length = variant.Length.Value,
                    Width = variant.Width.Value,
                    Height = variant.Height.Value,
                },
                StringComparer.Ordinal);

        return Edit(part, context);
    }

    private static bool IsSize(decimal? value)
        => value is null or (>= 0 and <= MaxSize);

    private static ShippingDimensions Dimensions(ShippingPart part, string variantId)
        => part.VariantDimensions?.TryGetValue(variantId, out var dimensions) == true ? dimensions : null;

    private static IEnumerable<(string VariantId, string Title)> GetVariants(ShippingPart part)
    {
        if (part.ContentItem?.TryGet<ProductVariantsPart>(out var variants) != true || !variants.HasVariants)
        {
            return [];
        }

        var optionNames = variants.Options?.Count > 0 ? variants.Options.Select(option => option.Name).ToArray() : null;

        return variants.Variants
            .Where(variant => !string.IsNullOrEmpty(variant.VariantId))
            .Select(variant => (variant.VariantId, variant.GetTitle(optionNames)));
    }
}
