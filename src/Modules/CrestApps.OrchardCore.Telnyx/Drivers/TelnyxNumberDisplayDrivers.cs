using CrestApps.OrchardCore.Telnyx.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telnyx.Drivers;

/// <summary>
/// Displays one of the account's numbers in the admin list.
/// </summary>
public sealed class TelnyxOwnedNumberDisplayDriver : DisplayDriver<TelnyxOwnedNumber>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(TelnyxOwnedNumber number, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("TelnyxOwnedNumber_Fields_SummaryAdmin", number)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("TelnyxOwnedNumber_DefaultTags_SummaryAdmin", number)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("TelnyxOwnedNumber_Buttons_SummaryAdmin", number).Location("Actions:5"),
            View("TelnyxOwnedNumber_ActionsMenu_SummaryAdmin", number).Location("ActionsMenu:10"));
    }
}

/// <summary>
/// Displays a number for sale in the search results.
/// </summary>
public sealed class TelnyxAvailableNumberDisplayDriver : DisplayDriver<TelnyxAvailableNumber>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(TelnyxAvailableNumber number, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("TelnyxAvailableNumber_Fields_SummaryAdmin", number)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Number:1"),
            View("TelnyxAvailableNumber_DefaultTags_SummaryAdmin", number)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Cost:5"),
            View("TelnyxAvailableNumber_Buttons_SummaryAdmin", number).Location("Actions:5"));
    }
}
