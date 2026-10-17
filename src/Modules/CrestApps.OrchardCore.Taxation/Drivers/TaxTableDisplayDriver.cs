using CrestApps.OrchardCore.Taxation.Models;
using CrestApps.OrchardCore.Taxation.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Taxation.Drivers;

internal sealed class TaxTableDisplayDriver : DisplayDriver<TaxTable>
{
    public override Task<IDisplayResult> DisplayAsync(TaxTable table, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = table.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("TaxTable_Fields_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("TaxTable_DefaultTags_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("TaxTable_Buttons_SummaryAdmin", table).Location("Actions:5"),
            View("TaxTable_DefaultMeta_SummaryAdmin", table)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Modified:5")
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
