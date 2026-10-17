using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OpportunityStageDisplayDriver : DisplayDriver<OpportunityStage>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityStageDisplayDriver(IStringLocalizer<OpportunityStageDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(OpportunityStage entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("OpportunityStage_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("OpportunityStage_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(entry.Description))),
            View("OpportunityStage_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            View("OpportunityStage_DefaultMeta_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Probability:5"),
            View("OpportunityStage_Buttons_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:5"));
    }

    public override IDisplayResult Edit(OpportunityStage entry, BuildEditorContext context)
    {
        return Initialize<OpportunityStageViewModel>("OpportunityStageFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = entry.Name;
            model.Description = entry.Description;
            model.Order = entry.Order;
            model.Probability = entry.Probability;
            model.IsClosed = entry.IsClosed;
            model.IsWon = entry.IsWon;
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(OpportunityStage entry, UpdateEditorContext context)
    {
        var model = new OpportunityStageViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            entry.Name = model.Name?.Trim();
        }

        entry.Description = model.Description?.Trim();
        entry.Order = model.Order;
        entry.Probability = model.Probability;
        entry.IsClosed = model.IsClosed;
        entry.IsWon = model.IsWon;

        return Edit(entry, context);
    }
}
