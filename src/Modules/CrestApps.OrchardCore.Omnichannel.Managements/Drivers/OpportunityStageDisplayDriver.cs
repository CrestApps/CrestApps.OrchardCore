using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
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
        return CombineAsync(
            View("OpportunityStage_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("OpportunityStage_Buttons_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"));
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
