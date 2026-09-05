using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Omnichannel.Sms.Portal.Drivers;

/// <summary>
/// Contributes the SMS inbound-routing editor to the channel-endpoint screen, so an SMS number's routing
/// (Agent/Queue target) is managed on the same screen as the number and its provider. Only shown for SMS
/// endpoints; the routing is stored in the endpoint's extensible properties.
/// </summary>
/// <remarks>
/// The Agent target is selected as a <b>user</b> (via the reusable user picker) for a good operator experience,
/// but the router and the rest of the workspace key off the operator's <c>AgentProfile</c>. This driver bridges
/// the two: on save it resolves (creating if needed) the selected user's agent profile and stores that profile
/// id; on edit it resolves the stored profile id back to the user so the picker shows the right person.
/// </remarks>
public sealed class SmsEndpointRoutingDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IAgentProfileManager _agentProfileManager;
    private readonly IShellFeaturesManager _shellFeaturesManager;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public SmsEndpointRoutingDisplayDriver(
        IAgentProfileManager agentProfileManager,
        IShellFeaturesManager shellFeaturesManager,
        IClock clock,
        IStringLocalizer<SmsEndpointRoutingDisplayDriver> stringLocalizer)
    {
        _agentProfileManager = agentProfileManager;
        _shellFeaturesManager = shellFeaturesManager;
        _clock = clock;
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return null;
        }

        return Initialize<SmsEndpointRoutingViewModel>("SmsEndpointRouting_Edit", async model =>
        {
            var routing = endpoint.GetOrCreate<SmsEndpointRoutingSettings>();

            var routedDistributionEnabled = await IsRoutedDistributionEnabledAsync();

            model.TargetType = routing.TargetType;
            model.AutoReplyMessage = routing.AutoReplyMessage;

            // A stored "Routed" mode on a tenant that no longer runs push distribution behaves as a shared pool
            // (the inbound chain already falls back that way), so the editor shows what actually happens.
            model.DistributionMode = routing.DistributionMode == SmsNumberRouteDistributionMode.Routed && !routedDistributionEnabled
                ? SmsNumberRouteDistributionMode.SharedPool
                : routing.DistributionMode;

            if (!string.IsNullOrEmpty(routing.TargetId))
            {
                if (routing.TargetType == SmsNumberRouteTargetType.Agent)
                {
                    var profile = await _agentProfileManager.FindByIdAsync(routing.TargetId);
                    model.AgentUserId = profile?.UserId;
                }
                else
                {
                    model.QueueId = routing.TargetId;
                }
            }

            model.TargetTypes =
            [
                new(S["Agent (personal number)"], nameof(SmsNumberRouteTargetType.Agent)),
                new(S["Queue (department)"], nameof(SmsNumberRouteTargetType.Queue)),
            ];
            model.DistributionModes = routedDistributionEnabled
                ?
                [
                    new SelectListItem(S["Shared pool (claim to own)"], nameof(SmsNumberRouteDistributionMode.SharedPool)),
                    new SelectListItem(S["Routed (assign via routing strategy)"], nameof(SmsNumberRouteDistributionMode.Routed)),
                ]
                :
                [
                    new SelectListItem(S["Shared pool (claim to own)"], nameof(SmsNumberRouteDistributionMode.SharedPool)),
                ];
        }).Location("Content:5");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new SmsEndpointRoutingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        string targetId = null;

        if (model.TargetType == SmsNumberRouteTargetType.Agent)
        {
            targetId = await ResolveAgentProfileIdAsync(model.AgentUserId?.Trim());
        }
        else if (model.TargetType == SmsNumberRouteTargetType.Queue)
        {
            targetId = model.QueueId?.Trim();
        }

        var distributionMode = model.DistributionMode;

        if (distributionMode == SmsNumberRouteDistributionMode.Routed && !await IsRoutedDistributionEnabledAsync())
        {
            distributionMode = SmsNumberRouteDistributionMode.SharedPool;
        }

        endpoint.Put(new SmsEndpointRoutingSettings
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

        return features.Any(feature => string.Equals(feature.Id, SmsPortalConstants.Feature.RoutedDistribution, StringComparison.Ordinal));
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

    private static bool IsSms(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);
}
