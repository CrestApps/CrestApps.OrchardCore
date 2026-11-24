using CrestApps.OrchardCore.Omnichannel.Messaging.Email.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.Drivers;

/// <summary>
/// Builds the row of an address in the email suppression list.
/// </summary>
internal sealed class EmailSuppressionDisplayDriver : DisplayDriver<EmailSuppression>
{
    public override Task<IDisplayResult> DisplayAsync(EmailSuppression suppression, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = suppression.Address ?? string.Empty;
        }

        return CombineAsync(
            View("EmailSuppression_Checkbox_SummaryAdmin", suppression)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Checkbox:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Select:1"),
            View("EmailSuppression_Fields_SummaryAdmin", suppression)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Address:1"),
            View("EmailSuppression_Description_SummaryAdmin", suppression)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Address:5"),
            View("EmailSuppression_DefaultTags_SummaryAdmin", suppression)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Reason:5"),
            View("EmailSuppression_DefaultMeta_SummaryAdmin", suppression)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Since:5"),
            View("EmailSuppression_Buttons_SummaryAdmin", suppression).Location("Actions:5"));
    }
}
