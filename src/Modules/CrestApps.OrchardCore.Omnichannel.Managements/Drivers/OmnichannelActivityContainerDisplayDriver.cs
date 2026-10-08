using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;
using CrestApps.OrchardCore.Users;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Drivers;

internal sealed class OmnichannelActivityContainerDisplayDriver : DisplayDriver<OmnichannelActivityContainer>
{
    private readonly ActivityHandlerDescriber _handlerDescriber;
    private readonly IDisplayNameProvider _displayNameProvider;

    public OmnichannelActivityContainerDisplayDriver(
        ActivityHandlerDescriber handlerDescriber,
        IDisplayNameProvider displayNameProvider)
    {
        _handlerDescriber = handlerDescriber;
        _displayNameProvider = displayNameProvider;
    }

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
        Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainer_Handler_SummaryAdmin", async model =>
            await _handlerDescriber.DescribeHandlerAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.AssignedToId)))
            .Location("Meta:6"),
        View("OmnichannelActivityContainerScheduledActivity_Fields_SummaryAdmin", container)
            .Location("Content:1")
            .OnGroup("ScheduledActivity"),
        View("OmnichannelActivityContainerScheduledActivity_Description_SummaryAdmin", container)
            .Location("Description:1")
            .OnGroup("ScheduledActivity")
            .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Instructions))),
        View("OmnichannelActivityContainerScheduledActivity_Buttons_SummaryAdmin", container)
            .Location("Actions:5")
            .OnGroup("ScheduledActivity"),
        View("OmnichannelActivityContainerScheduledActivity_DefaultMeta_SummaryAdmin", container)
            .Location("Meta:5")
            .OnGroup("ScheduledActivity"),
        Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainer_Handler_SummaryAdmin", async model =>
            await _handlerDescriber.DescribeHandlerAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.AssignedToId)))
            .Location("Meta:6")
            .OnGroup("ScheduledActivity"),
        View("OmnichannelActivityContainerCompletedActivity_Fields_SummaryAdmin", container)
            .Location("Content:1")
            .OnGroup("CompletedActivity"),
        View("OmnichannelActivityContainerCompletedActivity_Buttons_SummaryAdmin", container)
            .Location("Actions:5")
            .OnGroup("CompletedActivity"),
        View("OmnichannelActivityContainerCompletedActivity_DefaultMeta_SummaryAdmin", container)
            .Location("Meta:5")
            .OnGroup("CompletedActivity"),
        Initialize<ActivityHandlerViewModel>("OmnichannelActivityContainerCompletedActivity_DispositionedBy_SummaryAdmin", async model =>
            await _handlerDescriber.DescribeDispositionAsync(model, container.Activity, await GetUserNameAsync(container, container.Activity.CompletedById)))
            .Location("Meta:6")
            .OnGroup("CompletedActivity"),
        View("OmnichannelActivityContainerCompletedActivity_Description_SummaryAdmin", container)
            .Location("Description:1")
            .OnGroup("CompletedActivity")
            .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(container.Activity.Notes)))
        );
    }

    // The lists load the row's user in one query for the whole page (the assignee of open work, the completing user of
    // completed work), so the name is only read from the container when it is the user asked about.
    private async Task<string> GetUserNameAsync(OmnichannelActivityContainer container, string userId)
    {
        if (container.User is null ||
            string.IsNullOrEmpty(userId) ||
            !string.Equals(container.User.UserId, userId, StringComparison.Ordinal))
        {
            return null;
        }

        return await _displayNameProvider.GetAsync(container.User);
    }
}
