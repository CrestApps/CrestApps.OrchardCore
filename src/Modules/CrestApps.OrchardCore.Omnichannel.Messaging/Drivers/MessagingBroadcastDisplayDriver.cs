using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Documents;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// The display-management driver for <see cref="MessagingBroadcast"/>: the row of the admin list of broadcasts.
/// </summary>
internal sealed class MessagingBroadcastDisplayDriver : DisplayDriver<MessagingBroadcast>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IDocumentManager<MessagingBroadcastQueueOutcomes> _queueOutcomes;

    internal readonly IStringLocalizer S;

    public MessagingBroadcastDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IDocumentManager<MessagingBroadcastQueueOutcomes> queueOutcomes,
        IStringLocalizer<MessagingBroadcastDisplayDriver> stringLocalizer)
    {
        _channelResolver = channelResolver;
        _queueOutcomes = queueOutcomes;
        S = stringLocalizer;
    }

    public override async Task<IDisplayResult> DisplayAsync(MessagingBroadcast broadcast, BuildDisplayContext context)
    {
        // A broadcast is only displayed as a row of the admin list of broadcasts.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        // The run's counts with what the outbox did with the messages the channel held back, which the outbox keeps in a
        // document of its own. The document is read once for the whole list.
        var outcome = (await _queueOutcomes.GetOrCreateImmutableAsync()).Find(broadcast.ItemId);
        var status = broadcast.GetDisplayStatus(outcome);

        // The row's classes and attributes are rendered on its row element, in both admin list layouts. The list is
        // searched client side, on the name, channel, sending address and status of each broadcast, the status as the
        // row shows it.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = $"{broadcast.Name} {broadcast.Channel} {broadcast.ServiceAddress} {status} {GetStatusText(status)}".ToLowerInvariant();
        }

        void Progress(BroadcastProgressViewModel model)
        {
            model.Broadcast = broadcast;
            model.Status = status;
            model.SentCount = broadcast.GetSentCount(outcome);
            model.QueuedCount = broadcast.GetQueuedCount(outcome);
            model.FailedCount = broadcast.GetFailedCount(outcome);
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
            Initialize<BroadcastProgressViewModel>("MessagingBroadcast_Status_SummaryAdmin", Progress)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:3")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            Initialize<BroadcastProgressViewModel>("MessagingBroadcast_Progress_SummaryAdmin", Progress)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Progress:1"),
            View("MessagingBroadcast_DefaultMeta_SummaryAdmin", broadcast)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Created:5"));
    }

    // The same words the status badge shows.
    private string GetStatusText(MessagingBroadcastStatus status)
    {
        return status switch
        {
            MessagingBroadcastStatus.Draft => S["Draft"],
            MessagingBroadcastStatus.Queued => S["Queued"],
            MessagingBroadcastStatus.Running => S["Sending"],
            MessagingBroadcastStatus.Completed => S["Completed"],
            MessagingBroadcastStatus.Failed => S["Failed"],
            _ => S["Unknown"],
        };
    }
}
