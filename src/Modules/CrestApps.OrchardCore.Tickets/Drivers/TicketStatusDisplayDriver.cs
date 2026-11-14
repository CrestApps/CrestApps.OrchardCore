using CrestApps.OrchardCore.Tickets.Models;
using CrestApps.OrchardCore.Tickets.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The row and the editor of a ticket status.
/// </summary>
internal sealed class TicketStatusDisplayDriver : TicketSettingsDisplayDriverBase<TicketStatus>
{
    /// <inheritdoc/>
    protected override string Column => "Category";

    /// <inheritdoc/>
    public override IDisplayResult Edit(TicketStatus status, BuildEditorContext context)
        => Initialize<TicketStatusViewModel>("TicketStatusFields_Edit", model =>
        {
            model.Name = status.Name;
            model.Category = status.Category;
            model.CustomerLabel = status.CustomerLabel;
            model.PausesTimers = status.PausesTimers;
            model.Color = status.Color;
            model.Order = status.Order;
        }).Location("Content:1");

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(TicketStatus status, UpdateEditorContext context)
    {
        var model = new TicketStatusViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        status.Name = model.Name?.Trim();
        status.Category = model.Category;
        status.CustomerLabel = string.IsNullOrWhiteSpace(model.CustomerLabel) ? null : model.CustomerLabel.Trim();
        status.PausesTimers = model.PausesTimers;
        status.Color = string.IsNullOrWhiteSpace(model.Color) ? null : model.Color.Trim();
        status.Order = model.Order;

        return Edit(status, context);
    }
}
