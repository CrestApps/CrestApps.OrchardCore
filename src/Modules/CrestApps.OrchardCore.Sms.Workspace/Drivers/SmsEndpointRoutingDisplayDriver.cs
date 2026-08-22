using CrestApps.Core;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using CrestApps.OrchardCore.Sms.Workspace.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Sms.Workspace.Drivers;

/// <summary>
/// Contributes the SMS inbound-routing editor to the channel-endpoint screen, so an SMS number's routing
/// (Agent/Queue target) is managed on the same screen as the number and its provider. Only shown for SMS
/// endpoints; the routing is stored in the endpoint's extensible properties.
/// </summary>
public sealed class SmsEndpointRoutingDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    internal readonly IStringLocalizer S;

    public SmsEndpointRoutingDisplayDriver(IStringLocalizer<SmsEndpointRoutingDisplayDriver> stringLocalizer)
    {
        S = stringLocalizer;
    }

    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return null;
        }

        return Initialize<SmsEndpointRoutingViewModel>("SmsEndpointRouting_Edit", model =>
        {
            var routing = endpoint.GetOrCreate<SmsEndpointRoutingSettings>();

            model.TargetType = routing.TargetType;
            model.TargetId = routing.TargetId;
            model.DistributionMode = routing.DistributionMode;
            model.AutoReplyMessage = routing.AutoReplyMessage;

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
        }).Location("Content:5#SMS routing;20");
    }

    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsSms(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new SmsEndpointRoutingViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        endpoint.Put(new SmsEndpointRoutingSettings
        {
            TargetType = model.TargetType,
            TargetId = model.TargetId?.Trim(),
            DistributionMode = model.DistributionMode,
            AutoReplyMessage = model.AutoReplyMessage?.Trim(),
        });

        return Edit(endpoint, context);
    }

    private static bool IsSms(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase);
}
