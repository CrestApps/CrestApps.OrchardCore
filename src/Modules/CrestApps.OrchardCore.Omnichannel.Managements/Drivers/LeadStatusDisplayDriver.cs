using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
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
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = entry.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("LeadStatus_Fields_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("LeadStatus_Description_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(entry.Description))),
            View("LeadStatus_DefaultTags_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Type:5"),
            View("LeadStatus_Buttons_SummaryAdmin", entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Actions:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Actions:5"));
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
            model.Kind = entry.IsConverted
                ? LeadStatusKind.Converted
                : entry.IsClosed ? LeadStatusKind.Closed : LeadStatusKind.Open;
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
        entry.IsConverted = model.Kind == LeadStatusKind.Converted;
        entry.IsClosed = model.Kind != LeadStatusKind.Open;

        // The default box is hidden, not cleared, when another type is picked, so it only counts for an open status.
        entry.IsDefault = model.IsDefault && model.Kind == LeadStatusKind.Open;

        return Edit(entry, context);
    }
}
