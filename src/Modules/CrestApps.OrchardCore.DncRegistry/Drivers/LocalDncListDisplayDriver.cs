using CrestApps.OrchardCore.DncRegistry.Models;
using CrestApps.OrchardCore.DncRegistry.ViewModels;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.DncRegistry.Drivers;

/// <summary>
/// Display driver that provides summary admin shapes for a single local DNC list entry.
/// </summary>
public sealed class LocalDncListDisplayDriver : DisplayDriver<LocalDncList>
{
    /// <summary>
    /// Builds the display shapes for a local DNC list entry in summary admin mode.
    /// </summary>
    /// <param name="entry">The local DNC list entry.</param>
    /// <param name="context">The build display context.</param>
    public override Task<IDisplayResult> DisplayAsync(LocalDncList entry, BuildDisplayContext context)
    {
        return CombineAsync(
            Initialize<LocalDncListViewModel>("LocalDncListMeta_SummaryAdmin", m => m.LocalDncList = entry)
                .Location("SummaryAdmin", "Meta:20"),
            Initialize<LocalDncListViewModel>("LocalDncListProgress_SummaryAdmin", m => m.LocalDncList = entry)
                .Location("SummaryAdmin", "Progress:5"),
            Initialize<LocalDncListViewModel>("LocalDncListActions_SummaryAdmin", m => m.LocalDncList = entry)
                .Location("SummaryAdmin", "ActionsMenu:10")
        );
    }
}
