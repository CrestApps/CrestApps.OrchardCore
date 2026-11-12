using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Displays one saved day of a worker in the My Work list of adherence days, in both admin list layouts.
/// </summary>
internal sealed class MyAdherenceDayRowDisplayDriver : DisplayDriver<MyAdherenceDayRow>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(MyAdherenceDayRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return Combine(
            View("MyAdherenceDayRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MyAdherenceDayRow_Adherence_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Adherence:5"),
            View("MyAdherenceDayRow_Conformance_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Conformance:5"),
            View("MyAdherenceDayRow_OutOfAdherence_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "OutOfAdherence:5"));
    }
}
