using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxTableDisplayDriver : DisplayDriver<TaxTable>
{
    public override Task<IDisplayResult> DisplayAsync(TaxTable table, BuildDisplayContext context)
    {
        return CombineAsync(
            View("TaxTable_Fields_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("TaxTable_Buttons_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"),
            View("TaxTable_DefaultMeta_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
        );
    }

    public override IDisplayResult Edit(TaxTable table, BuildEditorContext context)
    {
        return Initialize<TaxTableViewModel>("TaxTableFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = table.Name;
            model.EffectiveFromUtc = table.EffectiveFromUtc;
            model.EffectiveToUtc = table.EffectiveToUtc;

            model.Rows = table.Rows
                .Select(row => new TaxTableRowViewModel
                {
                    Minimum = row.Minimum,
                    Maximum = row.Maximum,
                    Rate = row.Rate,
                    FixedAmount = row.FixedAmount,
                    BaseAmount = row.BaseAmount,
                })
                .ToList();
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(TaxTable table, UpdateEditorContext context)
    {
        var model = new TaxTableViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            table.Name = model.Name?.Trim();
        }

        table.EffectiveFromUtc = model.EffectiveFromUtc;
        table.EffectiveToUtc = model.EffectiveToUtc;

        table.Rows = model.Rows?
            .Where(row => row is not null)
            .OrderBy(row => row.Minimum)
            .Select(row => new TaxTableRow
            {
                Minimum = row.Minimum,
                Maximum = row.Maximum,
                Rate = row.Rate,
                FixedAmount = row.FixedAmount,
                BaseAmount = row.BaseAmount,
            })
            .ToList() ?? [];

        return Edit(table, context);
    }
}
