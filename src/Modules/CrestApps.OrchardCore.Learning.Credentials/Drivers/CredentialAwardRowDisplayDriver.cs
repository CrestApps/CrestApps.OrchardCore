using CrestApps.OrchardCore.Learning.Credentials.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Learning.Credentials.Drivers;

/// <summary>
/// The row of the credential awards admin list, in the List and Grid layouts.
/// </summary>
internal sealed class CredentialAwardRowDisplayDriver : DisplayDriver<CredentialAwardRow>
{
    /// <inheritdoc/>
    public override Task<IDisplayResult> DisplayAsync(CredentialAwardRow row, BuildDisplayContext context)
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
            View("CredentialAwardRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Credential:1"),
            View("CredentialAwardRow_Tags_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("CredentialAwardRow_Meta_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Issued:5"),
            View("CredentialAwardRow_Buttons_SummaryAdmin", row).Location("Actions:5"),
            View("CredentialAwardRow_ActionsMenu_SummaryAdmin", row).Location("ActionsMenu:10"));
    }
}
