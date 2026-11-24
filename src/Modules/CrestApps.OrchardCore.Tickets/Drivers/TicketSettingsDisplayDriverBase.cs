using CrestApps.OrchardCore.Tickets.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Tickets.Drivers;

/// <summary>
/// The rows of a ticket settings list, in both admin list layouts: the name, one column of the record's own, the buttons
/// and the actions menu.
/// </summary>
/// <typeparam name="T">The record.</typeparam>
internal abstract class TicketSettingsDisplayDriverBase<T> : DisplayDriver<T>
    where T : CrestApps.Core.Models.CatalogItem, ITicketSettingsRecord
{
    /// <summary>
    /// Gets the name of the record's own column, which its views are named after.
    /// </summary>
    protected abstract string Column { get; }

    /// <inheritdoc/>
    public override IDisplayResult Display(T record, BuildDisplayContext context)
    {
        if (context.DisplayType is not OrchardCoreConstants.DisplayType.SummaryAdmin and not OrchardCoreConstants.DisplayType.SummaryAdminGrid)
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = record.Name?.ToLowerInvariant() ?? string.Empty;
        }

        var name = typeof(T).Name;

        return Combine(
            View($"{name}_Fields_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View($"{name}_{Column}_SummaryAdmin", record)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, $"{Column}:5"),
            View($"{name}_Buttons_SummaryAdmin", record).Location("Actions:5"),
            View($"{name}_ActionsMenu_SummaryAdmin", record).Location("ActionsMenu:10"));
    }
}
