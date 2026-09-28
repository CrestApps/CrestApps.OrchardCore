using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// Contributes the inbound-routing editor to the channel-endpoint screen of every messaging channel, so an
/// endpoint's routing (Agent/Queue target) is managed on the same screen as the endpoint and its provider. Only
/// shown for endpoints of an enabled messaging channel; the routing is stored in the endpoint's extensible
/// properties.
/// </summary>
/// <remarks>
/// The Agent target is selected as a <b>user</b> (via the reusable user picker) for a good operator experience,
/// but the router and the rest of the workspace key off the operator's <c>AgentProfile</c>. This driver bridges
/// the two: on save it resolves (creating if needed) the selected user's agent profile and stores that profile
/// id; on edit it resolves the stored profile id back to the user so the picker shows the right person.
/// </remarks>
public sealed class MessagingEndpointRoutingDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IActivityQueueManager _queueManager;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public MessagingEndpointRoutingDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IAgentProfileManager agentProfileManager,
        IEnumerable<IActivityQueueManager> queueManagers,
        IShellFeaturesManager shellFeaturesManager,
        IClock clock,
        IStringLocalizer<MessagingEndpointRoutingDisplayDriver> stringLocalizer)
    {
        _channelResolver = channelResolver;
        _agentProfileManager = agentProfileManager;
        // Queues are a feature of their own that the workspace does not require: without it the picker shows the id.
        _queueManager = queueManagers.FirstOrDefault();
        _shellFeaturesManager = shellFeaturesManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsMessagingEndpoint(endpoint))
        {
            return null;
        }

        return Initialize<EndpointRoutingViewModel>("MessagingEndpointRouting_Edit", async model =>
        {
            var routing = endpoint.GetOrCreate<MessagingEndpointRoutingSettings>();

            var routedDistributionEnabled = await IsRoutedDistributionEnabledAsync();

            model.TargetType = routing.TargetType;
            model.AutoReplyMessage = routing.AutoReplyMessage;

            // A stored "Routed" mode on a tenant that no longer runs push distribution behaves as a shared pool
            // (the inbound chain already falls back that way), so the editor shows what actually happens.
            model.DistributionMode = routing.DistributionMode == ConversationDistributionMode.Routed && !routedDistributionEnabled
                ? ConversationDistributionMode.SharedPool
                : routing.DistributionMode;

            if (!string.IsNullOrEmpty(routing.TargetId))
            {
                if (routing.TargetType == ConversationRouteTargetType.Agent)
                {
                    var profile = await _agentProfileManager.FindByIdAsync(routing.TargetId);
                    model.AgentUserId = profile?.UserId;
                }
                else
                {
                    model.QueueId = routing.TargetId;

                    // The picker shows the queue by its name, not its identifier.
                    model.QueueName = _queueManager is null ? null : (await _queueManager.FindByIdAsync(routing.TargetId))?.Name;
                }
            }

            model.TargetTypes =
            [
                new(S["Agent (personal number)"], nameof(ConversationRouteTargetType.Agent)),
                new(S["Queue (department)"], nameof(ConversationRouteTargetType.Queue)),
            ];
            model.DistributionModes = routedDistributionEnabled
                ?
                [
                    new SelectListItem(S["Shared pool (claim to own)"], nameof(ConversationDistributionMode.SharedPool)),
                    new SelectListItem(S["Routed (assign via routing strategy)"], nameof(ConversationDistributionMode.Routed)),
                ]
                :
                [
                    new SelectListItem(S["Shared pool (claim to own)"], nameof(ConversationDistributionMode.SharedPool)),
                ];
        }).Location("Content:1%Inbound routing;2");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsMessagingEndpoint(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new EndpointRoutingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        string targetId = null;

        if (model.TargetType == ConversationRouteTargetType.Agent)
        {
            targetId = await ResolveAgentProfileIdAsync(model.AgentUserId?.Trim());
        }
        else if (model.TargetType == ConversationRouteTargetType.Queue)
        {
            targetId = model.QueueId?.Trim();
        }

        var distributionMode = model.DistributionMode;

        if (distributionMode == ConversationDistributionMode.Routed && !await IsRoutedDistributionEnabledAsync())
        {
            distributionMode = ConversationDistributionMode.SharedPool;
        }

        endpoint.Put(new MessagingEndpointRoutingSettings
        {
            TargetType = model.TargetType,
            TargetId = targetId,
            DistributionMode = distributionMode,
            AutoReplyMessage = model.AutoReplyMessage?.Trim(),
        });

        return Edit(endpoint, context);
    }

    private async Task<bool> IsRoutedDistributionEnabledAsync()
    {
        var features = await _shellFeaturesManager.GetEnabledFeaturesAsync();

        return features.Any(feature => string.Equals(feature.Id, MessagingConstants.Feature.RoutedDistribution, StringComparison.Ordinal));
    }

    // The selected agent is stored by its agent-profile id. Ensure the operator has a profile (creating a bare
    // one if this is the first time they are referenced) so inbound routing can resolve the assignment.
    private async Task<string> ResolveAgentProfileIdAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return null;
        }

        var profile = await _agentProfileManager.FindByUserIdAsync(userId);

        if (profile is null)
        {
            profile = await _agentProfileManager.NewAsync();
            profile.UserId = userId;
            profile.Name = userId;
            profile.CreatedUtc = _clock.UtcNow;

            await _agentProfileManager.CreateAsync(profile);
        }

        return profile.ItemId;
    }

    private bool IsMessagingEndpoint(OmnichannelChannelEndpoint endpoint)
        => _channelResolver.Get(endpoint.Channel) is not null;
}
