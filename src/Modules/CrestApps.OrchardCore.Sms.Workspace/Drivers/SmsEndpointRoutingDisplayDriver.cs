using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Sms.Workspace.Drivers;

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
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public SmsEndpointRoutingDisplayDriver(
        IAgentProfileManager agentProfileManager,
        IClock clock,
        IStringLocalizer<SmsEndpointRoutingDisplayDriver> stringLocalizer)
    {
        _agentProfileManager = agentProfileManager;
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

            model.TargetType = routing.TargetType;
            model.DistributionMode = routing.DistributionMode;
            model.AutoReplyMessage = routing.AutoReplyMessage;

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
            model.DistributionModes =
            [
                new(S["Shared pool (claim to own)"], nameof(SmsNumberRouteDistributionMode.SharedPool)),
                new(S["Routed (assign via routing strategy)"], nameof(SmsNumberRouteDistributionMode.Routed)),
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

        endpoint.Put(new SmsEndpointRoutingSettings
        {
            TargetType = model.TargetType,
            TargetId = targetId,
            DistributionMode = model.DistributionMode,
            AutoReplyMessage = model.AutoReplyMessage?.Trim(),
        });

        return Edit(endpoint, context);
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
