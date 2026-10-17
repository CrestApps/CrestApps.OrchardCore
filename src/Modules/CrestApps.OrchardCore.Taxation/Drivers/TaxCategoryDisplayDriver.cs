using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxCategoryDisplayDriver : DisplayDriver<TaxCategory>
{
    public override Task<IDisplayResult> DisplayAsync(TaxCategory category, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = category.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("TaxCategory_Fields_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TaxCategory_DefaultTags_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("TaxCategory_Description_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(category.Description))),
            View("TaxCategory_Buttons_SummaryAdmin", category).Location("Actions:5"),
            View("TaxCategory_DefaultMeta_SummaryAdmin", category)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
        );
    }

    public override IDisplayResult Edit(TaxCategory category, BuildEditorContext context)
    {
        return Initialize<TaxCategoryViewModel>("TaxCategoryFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = category.Name;
            model.Code = category.Code;
            model.ParentCode = category.ParentCode;
            model.Description = category.Description;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxCategory category, UpdateEditorContext context)
    {
        var model = new TaxCategoryViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            category.Name = model.Name?.Trim();
        }

        category.Code = model.Code?.Trim();
        category.ParentCode = model.ParentCode?.Trim();
        category.Description = model.Description?.Trim();

        return Edit(category, context);
    }
}
