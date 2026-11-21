using CrestApps.OrchardCore.Learning.Xapi.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Xapi.Drivers;

/// <summary>
/// The row of a statement in the admin list of statements.
/// </summary>
internal sealed class XapiStatementRowDisplayDriver : DisplayDriver<XapiStatementRow>
{
    public override Task<IDisplayResult> DisplayAsync(XapiStatementRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return CombineAsync(
            View("XapiStatementRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Statement:1"),
            View("XapiStatementRow_Result_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Result:1"),
            View("XapiStatementRow_Stored_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Stored:5"),
            View("XapiStatementRow_Buttons_SummaryAdmin", row).Location("Actions:5"));
    }
}
