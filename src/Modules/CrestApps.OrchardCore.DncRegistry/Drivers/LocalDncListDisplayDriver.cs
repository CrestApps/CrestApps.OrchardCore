using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Display driver that provides summary admin shapes for a single local DNC list entry.
/// </summary>
public sealed class LocalDncListDisplayDriver : DisplayDriver<LocalDncList>
{
    /// <summary>
    /// Builds the display shapes for a local DNC list entry in the admin list, in its List and Grid layouts.
    /// </summary>
    /// <param name="entry">The local DNC list entry.</param>
    /// <param name="context">The build display context.</param>
    public override Task<IDisplayResult> DisplayAsync(LocalDncList entry, BuildDisplayContext context)
    {
        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = (string.IsNullOrWhiteSpace(entry.Name) ? entry.UploadedFileName : entry.Name)?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            Initialize<LocalDncListViewModel>("LocalDncListFields_SummaryAdmin", m => m.LocalDncList = entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<LocalDncListViewModel>("LocalDncListFileName_SummaryAdmin", m => m.LocalDncList = entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(ShowsFileName(entry))),
            Initialize<LocalDncListViewModel>("LocalDncListMeta_SummaryAdmin", m => m.LocalDncList = entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:20")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:20"),
            Initialize<LocalDncListViewModel>("LocalDncListProgress_SummaryAdmin", m => m.LocalDncList = entry)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:10"),
            Initialize<LocalDncListViewModel>("LocalDncListActions_SummaryAdmin", m => m.LocalDncList = entry)
                .Location("ActionsMenu:10")
        );
    }

    // The name the user typed is the list's title. The uploaded file name is only a fallback for a list that has no
    // name, and is otherwise shown under the title so the source file can still be identified.
    private static bool ShowsFileName(LocalDncList entry)
        => !string.IsNullOrWhiteSpace(entry.Name)
            && !string.IsNullOrWhiteSpace(entry.UploadedFileName)
            && !string.Equals(entry.Name, entry.UploadedFileName, StringComparison.OrdinalIgnoreCase);
}
