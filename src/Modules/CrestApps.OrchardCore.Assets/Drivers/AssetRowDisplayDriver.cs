using CrestApps.OrchardCore.Assets.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Assets.Drivers;

/// <summary>
/// Displays a row of the list, in both layouts.
/// </summary>
internal sealed class AssetRowDisplayDriver : DisplayDriver<AssetRow>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(AssetRow item, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape row)
        {
            row.Classes.Add("item");
        }

        return Combine(
            View("AssetRow_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("AssetRow_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("AssetRow_Buttons_SummaryAdmin", item).Location("Actions:5"));
    }
}
