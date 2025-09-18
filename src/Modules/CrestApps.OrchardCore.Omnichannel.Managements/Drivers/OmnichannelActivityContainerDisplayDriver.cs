using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityContainerDisplayDriver : DisplayDriver<OmnichannelActivityContainer>
{
    public override Task<IDisplayResult> DisplayAsync(OmnichannelActivityContainer container, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelActivityContainer_Fields_SummaryAdmin", container).Location("Content:1"),
            View("OmnichannelActivityContainer_Buttons_SummaryAdmin", container).Location("Actions:5"),
            View("OmnichannelActivityContainer_DefaultMeta_SummaryAdmin", container).Location("Meta:5")
        );
    }
}
