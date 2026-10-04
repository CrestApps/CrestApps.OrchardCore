using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// Provides the default implementation of <see cref="IMessagingInboundRoutingResolver"/>: the enabled entry point that
/// picked the address for the channel, with its opening hours applied to the automatic reply.
/// </summary>
public sealed class MessagingInboundRoutingResolver : IMessagingInboundRoutingResolver
{
    private readonly IContactCenterEntryPointManager _entryPointManager;
    private readonly IBusinessHoursService _businessHours;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingInboundRoutingResolver"/> class.
    /// </summary>
    /// <param name="entryPointManager">The entry points.</param>
    /// <param name="businessHours">The business-hours service, for whether an entry point is open.</param>
    public MessagingInboundRoutingResolver(
        IContactCenterEntryPointManager entryPointManager,
        IBusinessHoursService businessHours)
    {
        _entryPointManager = entryPointManager;
        _businessHours = businessHours;
    }

    /// <inheritdoc/>
    public async Task<MessagingInboundRouting> ResolveAsync(OmnichannelChannelEndpoint address, string channel, CancellationToken cancellationToken = default)
    {
        if (address is null || string.IsNullOrEmpty(channel))
        {
            return null;
        }

        var entryPoint = (await _entryPointManager.GetEnabledAsync(cancellationToken))
            .FirstOrDefault(entryPoint =>
                string.Equals(entryPoint.GetChannel(), channel, StringComparison.OrdinalIgnoreCase) &&
                (entryPoint.AddressIds ?? []).Any(address.IsKnownAs));

        if (entryPoint is null)
        {
            return null;
        }

        var settings = entryPoint.TryGet<MessagingEntryPointSettings>(out var stored) && stored is not null
            ? stored
            : new MessagingEntryPointSettings();

        var isOpen = string.IsNullOrEmpty(entryPoint.BusinessHoursCalendarId) ||
            await _businessHours.IsOpenAsync(entryPoint.BusinessHoursCalendarId, cancellationToken);

        var isAgent = entryPoint.TargetType == EntryPointTargetType.Agent;
        var isAIAgent = entryPoint.TargetType == EntryPointTargetType.AIAgent;

        return new MessagingInboundRouting
        {
            EntryPointId = entryPoint.ItemId,
            TargetType = isAgent ? ConversationRouteTargetType.Agent : ConversationRouteTargetType.Queue,
            // An AI agent names no person or queue, so whatever it does not answer lands in the shared inbox.
            TargetId = isAgent ? entryPoint.TargetAgentId : isAIAgent ? null : entryPoint.TargetQueueId,
            AIProfileId = isAIAgent && isOpen ? entryPoint.TargetAIProfileId : null,
            DistributionMode = settings.DistributionMode,
            IsOpen = isOpen,
            AutoReplyMessage = !isOpen && !string.IsNullOrWhiteSpace(settings.ClosedAutoReplyMessage)
                ? settings.ClosedAutoReplyMessage.Trim()
                : settings.AutoReplyMessage?.Trim(),
        };
    }
}
