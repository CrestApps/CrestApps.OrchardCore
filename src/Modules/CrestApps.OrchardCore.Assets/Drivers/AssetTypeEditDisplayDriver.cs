using CrestApps.OrchardCore.Assets.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Assets.Drivers;

/// <summary>
/// Builds the editor of the asset type page: its details, attributes and meters, each in its card.
/// </summary>
internal sealed class AssetTypeEditDisplayDriver : DisplayDriver<AssetTypeEditViewModel>
{
    internal readonly IStringLocalizer S;

    public AssetTypeEditDisplayDriver(IStringLocalizer<AssetTypeEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(AssetTypeEditViewModel model, BuildEditorContext context)
        => Combine(
            Initialize<AssetTypeEditViewModel>("AssetTypeDetails_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Details"], "10")),
            Initialize<AssetTypeEditViewModel>("AssetTypeAttributes_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Attributes"], "20")),
            Initialize<AssetTypeEditViewModel>("AssetTypeMeters_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "3").Card(S["Meters"], "30")));

    public override async Task<IDisplayResult> UpdateAsync(AssetTypeEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(model, Prefix, m => m.Name, m => m.Category, m => m.Description, m => m.Attributes, m => m.Meters);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller binds and validates.
    protected override void BuildPrefix(AssetTypeEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(AssetTypeEditViewModel source, AssetTypeEditViewModel model)
    {
        model.Id = source.Id;
        model.Name = source.Name;
        model.Category = source.Category;
        model.Description = source.Description;
        model.Attributes = source.Attributes;
        model.Meters = source.Meters;
    }
}
