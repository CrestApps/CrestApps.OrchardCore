using CrestApps.OrchardCore.WorkOrders.Agreements.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.WorkOrders.Agreements.Drivers;

/// <summary>
/// Displays a row of the agreement list, in both layouts.
/// </summary>
internal sealed class ServiceAgreementRowDisplayDriver : DisplayDriver<ServiceAgreementRow>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(ServiceAgreementRow item, BuildDisplayContext context)
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
            View("ServiceAgreementRow_Fields_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ServiceAgreementRow_Tags_SummaryAdmin", item)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:5"),
            View("ServiceAgreementRow_Buttons_SummaryAdmin", item).Location("Actions:5"));
    }
}
