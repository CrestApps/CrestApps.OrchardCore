using CrestApps.OrchardCore.Companies.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Companies.Drivers;

/// <summary>
/// Renders a company as a row of the companies admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class CompanyDisplayDriver : DisplayDriver<Company>
{
    public override IDisplayResult Display(Company company, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = company.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Company_Fields_SummaryAdmin", company)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Company_Description_SummaryAdmin", company)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("Company_Status_SummaryAdmin", company)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Company_Buttons_SummaryAdmin", company).Location("Actions:5"));
    }
}
