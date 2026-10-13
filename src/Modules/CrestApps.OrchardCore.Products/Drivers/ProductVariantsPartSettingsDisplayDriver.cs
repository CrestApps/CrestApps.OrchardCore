using CrestApps.OrchardCore.Products.Core.Models;
using CrestApps.OrchardCore.Products.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Edits the options a product type's variants are made of, such as Size and Color, and whether listings can
/// filter on each.
/// </summary>
public sealed class ProductVariantsPartSettingsDisplayDriver : ContentTypePartDefinitionDisplayDriver<ProductVariantsPart>
{
    // Rows left blank for adding options without a script.
    private const int BlankRows = 2;

    internal readonly IStringLocalizer S;

    public ProductVariantsPartSettingsDisplayDriver(IStringLocalizer<ProductVariantsPartSettingsDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(ContentTypePartDefinition contentTypePartDefinition, BuildEditorContext context)
    {
        return Initialize<ProductVariantsPartSettingsViewModel>("ProductVariantsPartSettings_Edit", model =>
        {
            var settings = contentTypePartDefinition.GetSettings<ProductVariantsPartSettings>();

            model.Options = (settings.Options ?? [])
                .Select(option => new ProductVariantOptionSettingsViewModel { Name = option.Name, Filterable = option.Filterable })
                .ToList();

            for (var i = 0; i < BlankRows; i++)
            {
                model.Options.Add(new ProductVariantOptionSettingsViewModel());
            }
        }).Location("Content");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentTypePartDefinition contentTypePartDefinition, UpdateTypePartEditorContext context)
    {
        var model = new ProductVariantsPartSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        var options = new List<ProductVariantOptionSettings>();

        foreach (var option in model.Options ?? [])
        {
            var name = option?.Name?.Trim();

            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            if (options.Any(existing => string.Equals(existing.Name, name, StringComparison.OrdinalIgnoreCase)))
            {
                context.Updater.ModelState.AddModelError(Prefix, nameof(model.Options), S["The option '{0}' is listed more than once.", name]);
                continue;
            }

            options.Add(new ProductVariantOptionSettings { Name = name, Filterable = option.Filterable });
        }

        context.Builder.WithSettings(new ProductVariantsPartSettings { Options = options });

        return Edit(contentTypePartDefinition, context);
    }
}
