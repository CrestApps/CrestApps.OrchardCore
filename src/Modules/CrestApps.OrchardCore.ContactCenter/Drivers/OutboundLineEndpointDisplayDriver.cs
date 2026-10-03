using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.ViewModels;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.ContactCenter.Drivers;

/// <summary>
/// Adds the outbound line to a phone number's editor: the agents who dial out from that number. That an agent is on
/// one line only is enforced by <see cref="Services.OutboundLineEndpointRule"/>, so a recipe is held to it too.
/// </summary>
public sealed class OutboundLineEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    /// <inheritdoc/>
    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        if (!IsPhoneEndpoint(endpoint))
        {
            return null;
        }

        return Initialize<OutboundLineEndpointViewModel>("OutboundLineEndpoint_Edit", model =>
        {
            model.UserIds = [.. ChannelEndpointOutboundLineResolver.GetUserIds(endpoint)];
        }).Location("Content:2%Voice calls;2");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (!IsPhoneEndpoint(endpoint))
        {
            return Edit(endpoint, context);
        }

        var model = new OutboundLineEndpointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        endpoint.Put(new OutboundLineSettings
        {
            UserIds = (model.UserIds ?? [])
                .Where(userId => !string.IsNullOrWhiteSpace(userId))
                .Select(userId => userId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
        });

        return Edit(endpoint, context);
    }

    // Shown on every phone number and kept visible by the editor only while the number is used for calls, so ticking
    // Voice calls on a new number shows it straight away.
    private static bool IsPhoneEndpoint(OmnichannelChannelEndpoint endpoint)
        => string.Equals(endpoint?.GetAddressType(), OmnichannelAddressTypes.PhoneNumber, StringComparison.OrdinalIgnoreCase);
}
