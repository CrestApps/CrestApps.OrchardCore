using CrestApps.OrchardCore.Subscriptions.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the row of a site in its buyer's own sites (My Sites), for both admin list layouts.
/// </summary>
public sealed class CustomerSiteDisplayDriver : DisplayDriver<CustomerSite>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(CustomerSite site, BuildDisplayContext context)
    {
        var job = site.Job;

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.DisplayType is OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid
            && context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = (string.IsNullOrEmpty(job.TenantTitle) ? job.TenantName : job.TenantTitle)?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("CustomerSite_Fields_SummaryAdmin", site)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("CustomerSite_Description_SummaryAdmin", site)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:5"),
            View("CustomerSite_Status_SummaryAdmin", site)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("CustomerSite_SiteName_SummaryAdmin", site)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "SiteName:5"),
            View("CustomerSite_Bought_SummaryAdmin", site)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Bought:5"),
        };

        if (job.Status == TenantProvisioningStatus.Succeeded && !string.IsNullOrEmpty(job.SiteUrl))
        {
            results.Add(View("CustomerSite_Open_SummaryAdmin", site).Location("Actions:5"));
        }

        return CombineAsync(results);
    }
}
