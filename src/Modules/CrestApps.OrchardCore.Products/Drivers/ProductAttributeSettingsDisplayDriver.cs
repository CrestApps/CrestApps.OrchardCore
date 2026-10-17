using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using CrestApps.OrchardCore.Products.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using Microsoft.Extensions.Localization;
using OrchardCore.Mvc.ModelBinding;
using CrestApps.OrchardCore.Products.Services;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Adds the "Product listing" settings to the fields of products: fields of a type a product attribute provider
/// can read, on a part that is attached to at least one content type with the product part.
/// </summary>
/// <remarks>
/// Fields belong to parts, and a part can be attached to several types, so the settings show when any of the
/// part's types is a product. A field of any other part keeps whatever settings it has; saving it leaves them as
/// they are.
/// </remarks>
public sealed class ProductAttributeSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver
{
    private readonly IEnumerable<IProductAttributeValueProvider> _providers;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    internal readonly IStringLocalizer S;

    public ProductAttributeSettingsDisplayDriver(
        IEnumerable<IProductAttributeValueProvider> providers,
        IContentDefinitionManager contentDefinitionManager,
        IStringLocalizer<ProductAttributeSettingsDisplayDriver> stringLocalizer)
    {
        _providers = providers;
        _contentDefinitionManager = contentDefinitionManager;
        S = stringLocalizer;
    }

    public override bool CanHandleModel(ContentPartFieldDefinition model)
        => _providers.Any(provider => provider.CanHandle(model.FieldDefinition.Name));

    public override async Task<IDisplayResult> EditAsync(ContentPartFieldDefinition model, BuildEditorContext context)
    {
        if (!await IsProductFieldAsync(model))
        {
            return null;
        }

        return Initialize<ProductAttributeSettingsViewModel>("ProductAttributeSettings_Edit", viewModel =>
        {
            var settings = model.GetSettings<ProductAttributeSettings>();
            var kind = _providers.First(provider => provider.CanHandle(model.FieldDefinition.Name)).GetValueKind(model);

            viewModel.Filterable = settings.Filterable;
            viewModel.Sortable = settings.Sortable;
            viewModel.Searchable = settings.Searchable;
            viewModel.ShowOnCard = settings.ShowOnCard;
            viewModel.ShowInSpecifications = settings.ShowInSpecifications;
            viewModel.Label = settings.Label;
            viewModel.NumberFormat = settings.NumberFormat;
            viewModel.Suffix = settings.Suffix;
            viewModel.SwatchColors = ProductSwatchColors.Format(settings.SwatchColors);
            viewModel.TermColorField = settings.TermColorField;
            viewModel.CanSort = ProductListingService.IsSortable(kind);
            viewModel.IsRichText = kind == ProductAttributeValueKind.RichText;
            viewModel.IsNumber = kind == ProductAttributeValueKind.Number;
            viewModel.CanHaveSwatches = kind is ProductAttributeValueKind.Text or ProductAttributeValueKind.Term;
            viewModel.IsTaxonomy = kind == ProductAttributeValueKind.Term && model.FieldDefinition.Name == "TaxonomyField";
        }).Location("Content:20");
    }

    public override async Task<IDisplayResult> UpdateAsync(ContentPartFieldDefinition model, UpdatePartFieldEditorContext context)
    {
        if (!await IsProductFieldAsync(model))
        {
            return null;
        }

        var viewModel = new ProductAttributeSettingsViewModel();

        await context.Updater.TryUpdateModelAsync(viewModel, Prefix);

        var swatchColors = ProductSwatchColors.Parse(viewModel.SwatchColors, out var invalid);

        if (invalid.Count > 0)
        {
            context.Updater.ModelState.AddModelError(Prefix, nameof(viewModel.SwatchColors), S["These swatch colors could not be read: {0}. Write one per line, like Midnight = #191970 or Sky = light blue.", string.Join(", ", invalid)]);
        }

        context.Builder.WithSettings(new ProductAttributeSettings
        {
            Filterable = viewModel.Filterable,
            Sortable = viewModel.Sortable,
            Searchable = viewModel.Searchable,
            ShowOnCard = viewModel.ShowOnCard,
            ShowInSpecifications = viewModel.ShowInSpecifications,
            Label = viewModel.Label?.Trim(),
            NumberFormat = viewModel.NumberFormat?.Trim(),
            Suffix = viewModel.Suffix?.Trim(),
            SwatchColors = swatchColors,
            TermColorField = string.IsNullOrWhiteSpace(viewModel.TermColorField) ? null : viewModel.TermColorField.Trim(),
        });

        return await EditAsync(model, context);
    }

    private async Task<bool> IsProductFieldAsync(ContentPartFieldDefinition model)
    {
        var partName = model.PartDefinition?.Name;

        if (string.IsNullOrEmpty(partName))
        {
            return false;
        }

        return (await _contentDefinitionManager.ListTypeDefinitionsAsync()).Any(type =>
            type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)) &&
            type.Parts.Any(part => part.PartDefinition.Name == partName));
    }
}
