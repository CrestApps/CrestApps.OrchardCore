using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.DataConnectors.Drivers;

/// <summary>
/// Renders the row of a refresh in a data set's refresh history.
/// </summary>
internal sealed class DataSetRefreshRunDisplayDriver : DisplayDriver<DataSetRefreshRun>
{
    public override Task<IDisplayResult> DisplayAsync(DataSetRefreshRun run, BuildDisplayContext context)
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
            View("DataSetRefreshRun_Fields_SummaryAdmin", run)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Started:1"),
            View("DataSetRefreshRun_Tags_SummaryAdmin", run)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:5"),
            View("DataSetRefreshRun_Description_SummaryAdmin", run)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Details:1")
        );
    }
}
