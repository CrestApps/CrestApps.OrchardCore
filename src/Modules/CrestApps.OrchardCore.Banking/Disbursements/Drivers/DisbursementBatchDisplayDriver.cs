using CrestApps.OrchardCore.Banking.Disbursements.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Banking.Disbursements.Drivers;

/// <summary>
/// The rows of the list of disbursement batches.
/// </summary>
internal sealed class DisbursementBatchDisplayDriver : DisplayDriver<DisbursementBatch>
{
    public override Task<IDisplayResult> DisplayAsync(DisbursementBatch batch, BuildDisplayContext context)
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
            View("DisbursementBatch_Fields_SummaryAdmin", batch)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Batch:1"),
            View("DisbursementBatch_Description_SummaryAdmin", batch)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Total:5"),
            View("DisbursementBatch_Tags_SummaryAdmin", batch)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("DisbursementBatch_ActionsMenu_SummaryAdmin", batch)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "ActionsMenu:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "ActionsMenu:5"));
    }
}
