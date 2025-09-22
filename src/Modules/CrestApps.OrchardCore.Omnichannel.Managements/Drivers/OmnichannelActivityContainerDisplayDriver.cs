using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityContainerDisplayDriver : DisplayDriver<OmnichannelActivityContainer>
{
    public override Task<IDisplayResult> DisplayAsync(OmnichannelActivityContainer container, BuildDisplayContext context)
    {
        return CombineAsync(
            View("OmnichannelActivityContainer_Fields_SummaryAdmin", container)
                .Location("Content:1"),

            View("OmnichannelActivityContainer_Description_SummaryAdmin", container)
                .Location("Description:1")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Instructions))),

            View("OmnichannelActivityContainer_Buttons_SummaryAdmin", container)
                .Location("Actions:5"),

            View("OmnichannelActivityContainer_DefaultMeta_SummaryAdmin", container)
                .Location("Meta:5"),

            View("OmnichannelActivityContainerScheduledActivity_Fields_SummaryAdmin", container)
                .Location("Content:1")
                .OnGroup("ScheduledActivity"),

            View("OmnichannelActivityContainerScheduledActivity_Description_SummaryAdmin", container)
                .Location("Description:1")
                .OnGroup("ScheduledActivity")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Instructions))),

            View("OmnichannelActivityContainerScheduledActivity_Buttons_SummaryAdmin", container)
                .Location("Actions:5").OnGroup("ScheduledActivity"),

            View("OmnichannelActivityContainerScheduledActivity_DefaultMeta_SummaryAdmin", container)
                .Location("Meta:5").OnGroup("ScheduledActivity"),

            View("OmnichannelActivityContainerCompletedActivity_Fields_SummaryAdmin", container)
                .Location("Content:1")
                .OnGroup("CompletedActivity"),

            View("OmnichannelActivityContainerCompletedActivity_Buttons_SummaryAdmin", container)
                .Location("Actions:5").OnGroup("CompletedActivity"),

            View("OmnichannelActivityContainerCompletedActivity_DefaultMeta_SummaryAdmin", container)
                .Location("Meta:5")
                .OnGroup("CompletedActivity"),

           View("OmnichannelActivityContainerCompletedActivity_Description_SummaryAdmin", container)
                .Location("Description:1")
                .OnGroup("CompletedActivity")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Notes)))
        );
    }
}
