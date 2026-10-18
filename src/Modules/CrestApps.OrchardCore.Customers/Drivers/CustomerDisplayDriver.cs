using CrestApps.OrchardCore.Customers.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Customers.Drivers;

/// <summary>
/// Renders a customer as a row of the customers admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class CustomerDisplayDriver : DisplayDriver<Customer>
{
    public override Task<IDisplayResult> DisplayAsync(Customer customer, BuildDisplayContext context)
    {
        // A customer is only displayed as a row of the admin list.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = customer.GetName()?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("Customer_Fields_SummaryAdmin", customer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Customer_DefaultTags_SummaryAdmin", customer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Customer_Contact_SummaryAdmin", customer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Contact:1"),
            View("Customer_DefaultMeta_SummaryAdmin", customer)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "LastActivity:1"),
            View("Customer_Buttons_SummaryAdmin", customer).Location("Actions:5"));
    }
}
