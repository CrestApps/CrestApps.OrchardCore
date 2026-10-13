using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.ViewModels;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.ContentTypes.Editors;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Products.Drivers;

/// <summary>
/// Adds the "Product listing" settings to every field type a product attribute provider can read.
/// </summary>
public sealed class ProductAttributeSettingsDisplayDriver : ContentPartFieldDefinitionDisplayDriver
{
    private readonly IEnumerable<IProductAttributeValueProvider> _providers;

    public ProductAttributeSettingsDisplayDriver(IEnumerable<IProductAttributeValueProvider> providers)
    {
        _providers = providers;
    }

    public override bool CanHandleModel(ContentPartFieldDefinition model)
        => _providers.Any(provider => provider.CanHandle(model.FieldDefinition.Name));

    public override IDisplayResult Edit(ContentPartFieldDefinition model, BuildEditorContext context)
    {
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

        return Edit(model, context);
    }
}
