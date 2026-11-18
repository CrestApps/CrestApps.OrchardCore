using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a ticket priority.
/// </summary>
internal sealed class TicketPriorityDisplayDriver : TicketSettingsDisplayDriverBase<TicketPriority>
{
    /// <inheritdoc/>
    protected override string Column => "Rank";

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketPriority priority, BuildEditorContext context)
        => Initialize<TicketPriorityViewModel>("TicketPriorityFields_Edit", model =>
        {
            model.Name = priority.Name;
            model.Rank = priority.Rank;
            model.Color = priority.Color;
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketPriority priority, UpdateEditorContext context)
    {
        var model = new TicketPriorityViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        priority.Name = model.Name?.Trim();
        priority.Rank = model.Rank;
        priority.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();

        return Edit(priority, context);
    }
}
