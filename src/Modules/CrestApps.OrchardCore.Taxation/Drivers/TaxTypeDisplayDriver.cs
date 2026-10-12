using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxTypeDisplayDriver : DisplayDriver<TaxType>
{
    public override Task<IDisplayResult> DisplayAsync(TaxType type, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TaxType_Fields_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("TaxType_Buttons_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("TaxType_DefaultMeta_SummaryAdmin", type)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(TaxType type, BuildEditorContext context)
    {
        return Initialize<TaxTypeViewModel>("TaxTypeFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = type.Name;
            model.Description = type.Description;
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxType type, UpdateEditorContext context)
    {
        var model = new TaxTypeViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            type.Name = model.Name?.Trim();
        }

        type.Description = model.Description?.Trim();

        return Edit(type, context);
    }
}
