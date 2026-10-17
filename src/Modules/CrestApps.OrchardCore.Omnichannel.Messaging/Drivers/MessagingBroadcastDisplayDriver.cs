using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for <see cref="MessagingBroadcast"/>: the row of the admin list of broadcasts.
/// </summary>
internal sealed class MessagingBroadcastDisplayDriver : DisplayDriver<MessagingBroadcast>
{
    private readonly IMessagingChannelResolver _channelResolver;

    public MessagingBroadcastDisplayDriver(IMessagingChannelResolver channelResolver)
    {
        _channelResolver = channelResolver;
    }

    public override IDisplayResult Display(MessagingBroadcast broadcast, BuildDisplayContext context)
    {
        // A broadcast is only displayed as a row of the admin list of broadcasts.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts. The list is
        // searched client side, on the name, channel, sending address and status of each broadcast.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{broadcast.Name} {broadcast.Channel} {broadcast.ServiceAddress} {broadcast.Status}".ToLowerInvariant();
        }

        return Combine(
            View("MessagingBroadcast_Fields_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            Initialize<ChannelViewModel>("MessagingBroadcast_Channel_SummaryAdmin", model =>
            {
                var channel = _channelResolver.Get(broadcast.Channel);

                model.Name = broadcast.Channel;
                model.DisplayName = channel?.DisplayName.Value ?? broadcast.Channel;
                model.IconCssClass = channel?.IconCssClass;
            })
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Channel:1")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(broadcast.Channel))),
            View("MessagingBroadcast_From_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "From:1")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(broadcast.ServiceAddress))),
            View("MessagingBroadcast_Status_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:3")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("MessagingBroadcast_Progress_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Progress:1"),
            View("MessagingBroadcast_DefaultMeta_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Created:5"));
    }
}
