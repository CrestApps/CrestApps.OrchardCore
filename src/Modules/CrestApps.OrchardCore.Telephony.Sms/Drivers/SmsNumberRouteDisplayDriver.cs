using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Sms.Core.Models;
using CrestApps.OrchardCore.Telephony.Sms.Models;
using CrestApps.OrchardCore.Telephony.Sms.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Localization;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Telephony.Sms.Drivers;

/// <summary>
/// The display-management driver for <see cref="SmsNumberRoute"/>: the admin list row and the create/edit form.
/// </summary>
public sealed class SmsNumberRouteDisplayDriver : DisplayDriver<SmsNumberRoute>
{
    private readonly IOmnichannelChannelEndpointManager _endpointManager;

    internal readonly IStringLocalizer S;

    public SmsNumberRouteDisplayDriver(
        IOmnichannelChannelEndpointManager endpointManager,
        IStringLocalizer<SmsNumberRouteDisplayDriver> stringLocalizer)
    {
        _endpointManager = endpointManager;
        S = stringLocalizer;
    }

    public override IDisplayResult Display(SmsNumberRoute route, BuildDisplayContext context)
    {
        return View("SmsNumberRoute_Fields_SummaryAdmin", route)
            .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1");
    }

    public override IDisplayResult Edit(SmsNumberRoute route, BuildEditorContext context)
    {
        return Initialize<SmsNumberRouteViewModel>("SmsNumberRouteFields_Edit", async model =>
        {
            model.Name = route.Name;
            model.Description = route.Description;
            model.EndpointId = route.EndpointId;
            model.TargetType = route.TargetType;
            model.TargetId = route.TargetId;
            model.DistributionMode = route.DistributionMode;
            model.AutoReplyMessage = route.AutoReplyMessage;
            model.Enabled = route.Enabled;

            model.Endpoints = await BuildEndpointOptionsAsync(route.EndpointId);
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
        }).Location("Content:1");
    }

    public override async Task<IDisplayResult> UpdateAsync(SmsNumberRoute route, UpdateEditorContext context)
    {
        var model = new SmsNumberRouteViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        route.Name = model.Name?.Trim();
        route.Description = model.Description?.Trim();
        route.EndpointId = model.EndpointId;
        route.TargetType = model.TargetType;
        route.TargetId = model.TargetId?.Trim();
        route.DistributionMode = model.DistributionMode;
        route.AutoReplyMessage = model.AutoReplyMessage?.Trim();
        route.Enabled = model.Enabled;

        // Denormalize the DID from the bound endpoint so the inbound pipeline resolves the route by the number
        // it received on without an extra lookup.
        if (!string.IsNullOrEmpty(model.EndpointId))
        {
            var endpoint = await _endpointManager.FindByIdAsync(model.EndpointId);
            route.DialedNumber = endpoint?.Value;
        }
        else
        {
            route.DialedNumber = null;
        }

        return Edit(route, context);
    }

    private async Task<IEnumerable<SelectListItem>> BuildEndpointOptionsAsync(string selectedId)
    {
        var endpoints = await _endpointManager.GetAllAsync();

        return endpoints
            .Where(endpoint => string.Equals(endpoint.Channel, OmnichannelConstants.Channels.Sms, StringComparison.OrdinalIgnoreCase))
            .Select(endpoint => new SelectListItem
            {
                Text = string.IsNullOrEmpty(endpoint.DisplayText) ? endpoint.Value : $"{endpoint.DisplayText} ({endpoint.Value})",
                Value = endpoint.ItemId,
                Selected = endpoint.ItemId == selectedId,
            })
            .ToList();
    }
}
