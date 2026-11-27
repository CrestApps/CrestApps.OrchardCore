using CrestApps.OrchardCore.PointOfSale.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.PointOfSale.Drivers;

/// <summary>
/// Builds the editor of a department, whose fields are grouped in cards by their placement.
/// </summary>
internal sealed class PosDepartmentEditDisplayDriver : DisplayDriver<PosDepartmentEditViewModel>
{
    internal readonly IStringLocalizer S;

    public PosDepartmentEditDisplayDriver(IStringLocalizer<PosDepartmentEditDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(PosDepartmentEditViewModel model, BuildEditorContext context)
    {
        return Combine(
            Initialize<PosDepartmentEditViewModel>("PosDepartment_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "1").Card(S["Department"], "10")),
            Initialize<PosDepartmentEditViewModel>("PosDepartmentLimits_Edit", m => Populate(model, m))
                .Location(l => l.Zone("Content", "2").Card(S["Key and Limits"], "20")));
    }

    public override async Task<IDisplayResult> UpdateAsync(PosDepartmentEditViewModel model, UpdateEditorContext context)
    {
        await context.Updater.TryUpdateModelAsync(
            model,
            Prefix,
            m => m.Name,
            m => m.Number,
            m => m.TaxCategoryCode,
            m => m.Position,
            m => m.IsEnabled,
            m => m.Color,
            m => m.MinimumAmount,
            m => m.MaximumAmount);

        return Edit(model, context);
    }

    // The fields keep the names the page posts, which the controller validates and the page script selects.
    protected override void BuildPrefix(PosDepartmentEditViewModel model, string htmlFieldPrefix)
    {
        Prefix = htmlFieldPrefix ?? string.Empty;
    }

    private static void Populate(PosDepartmentEditViewModel source, PosDepartmentEditViewModel model)
    {
        model.ItemId = source.ItemId;
        model.HasTaxCategories = source.HasTaxCategories;
        model.TaxCategories = source.TaxCategories;
        model.Name = source.Name;
        model.Number = source.Number;
        model.TaxCategoryCode = source.TaxCategoryCode;
        model.Position = source.Position;
        model.IsEnabled = source.IsEnabled;
        model.Color = source.Color;
        model.MinimumAmount = source.MinimumAmount;
        model.MaximumAmount = source.MaximumAmount;
    }
}
