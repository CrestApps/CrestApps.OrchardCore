using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Subscriptions.Drivers;

/// <summary>
/// Builds the row of a provisioning job in the administration site provisioning list, for both admin list layouts.
/// </summary>
public sealed class TenantProvisioningJobDisplayDriver : DisplayDriver<TenantProvisioningJob>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(TenantProvisioningJob job, BuildDisplayContext context)
    {
        // A provisioning job is only displayed as a row of the list.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = job.TenantName?.ToLowerInvariant() ?? string.Empty;
        }

        var results = new List<IDisplayResult>
        {
            View("TenantProvisioningJob_Fields_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:1"),
            View("TenantProvisioningJob_Description_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Title:5"),
            View("TenantProvisioningJob_Status_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("TenantProvisioningJob_Owner_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Owner:5"),
            View("TenantProvisioningJob_Bought_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:10")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Bought:5"),
            View("TenantProvisioningJob_Attempts_SummaryAdmin", job)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:15")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Attempts:5"),
        };

        if (TenantProvisioningJobActions.CanRetry(job))
        {
            results.Add(View("TenantProvisioningJob_Retry_SummaryAdmin", job).Location("Actions:5"));
        }

        if (TenantProvisioningJobActions.CanMarkCreated(job))
        {
            results.Add(View("TenantProvisioningJob_MarkCreated_SummaryAdmin", job).Location("Actions:10"));
        }

        return CombineAsync(results);
    }
}
