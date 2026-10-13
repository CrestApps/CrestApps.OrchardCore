using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using CrestApps.OrchardCore.Products.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

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

    public ProductAttributeSettingsDisplayDriver(
        IEnumerable<IProductAttributeValueProvider> providers,
        IContentDefinitionManager contentDefinitionManager)
    {
        _providers = providers;
        _contentDefinitionManager = contentDefinitionManager;
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
            viewModel.CanSort = kind is ProductAttributeValueKind.Number or ProductAttributeValueKind.Date;
            viewModel.IsNumber = kind == ProductAttributeValueKind.Number;
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
