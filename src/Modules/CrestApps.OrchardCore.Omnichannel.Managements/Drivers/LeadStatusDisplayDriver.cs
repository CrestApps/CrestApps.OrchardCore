using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class LeadStatusDisplayDriver : DisplayDriver<LeadStatus>
{
    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDisplayDriver"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public LeadStatusDisplayDriver(IStringLocalizer<LeadStatusDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override Task<IDisplayResult> DisplayAsync(LeadStatus entry, BuildDisplayContext context)
    {
        return CombineAsync(
            View("LeadStatus_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1"),
            View("LeadStatus_Buttons_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5"));
    }

    public override IDisplayResult Edit(LeadStatus entry, BuildEditorContext context)
    {
        return Initialize<LeadStatusViewModel>("LeadStatusFields_Edit", model =>
        {
            model.IsNew = context.IsNew;
            model.Name = entry.Name;
            model.Description = entry.Description;
            model.Order = entry.Order;
            model.IsDefault = entry.IsDefault;
            model.IsClosed = entry.IsClosed;
            model.IsConverted = entry.IsConverted;
        }).Location("Content:1%General;1");
    }

    public override async Task<IDisplayResult> UpdateAsync(LeadStatus entry, UpdateEditorContext context)
    {
        var model = new LeadStatusViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        if (context.IsNew)
        {
            entry.Name = model.Name?.Trim();
        }

        entry.Description = model.Description?.Trim();
        entry.Order = model.Order;
        entry.IsDefault = model.IsDefault;
        entry.IsClosed = model.IsClosed;
        entry.IsConverted = model.IsConverted;

        // The converted status is always closed: a converted lead is never worked again.
        if (entry.IsConverted)
        {
            entry.IsClosed = true;
        }

        return Edit(entry, context);
    }
}
