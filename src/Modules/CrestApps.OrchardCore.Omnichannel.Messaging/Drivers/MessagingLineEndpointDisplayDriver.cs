using CrestApps.Core;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using CrestApps.OrchardCore.Omnichannel.Messaging.ViewModels;
using Microsoft.Extensions.Options;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Drivers;

/// <summary>
/// Adds the agents who text from a number to the number's messaging card, the counterpart of the agents who dial from
/// it on its voice card.
/// </summary>
internal sealed class MessagingLineEndpointDisplayDriver : DisplayDriver<OmnichannelChannelEndpoint>
{
    private readonly IMessagingChannelResolver _channelResolver;
    private readonly OmnichannelAddressOptions _addressOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingLineEndpointDisplayDriver"/> class.
    /// </summary>
    /// <param name="channelResolver">The enabled messaging channels.</param>
    /// <param name="addressOptions">The address types and capabilities.</param>
    public MessagingLineEndpointDisplayDriver(
        IMessagingChannelResolver channelResolver,
        IOptions<OmnichannelAddressOptions> addressOptions)
    {
        _channelResolver = channelResolver;
        _addressOptions = addressOptions.Value;
    }

    /// <inheritdoc/>
    public override IDisplayResult Edit(OmnichannelChannelEndpoint endpoint, BuildEditorContext context)
    {
        var channels = GetMessagingCapabilities(endpoint);

        if (channels.Count == 0)
        {
            return null;
        }

        return Initialize<MessagingLineEndpointViewModel>("MessagingLineEndpoint_Edit", model =>
        {
            model.TextingUserIds = [.. MessagingLines.GetUserIds(endpoint)];
            model.ServedCapabilities = string.Join(",", channels);
        }).Location("Content:2%Text messages;3");
    }

    /// <inheritdoc/>
    public override async Task<IDisplayResult> UpdateAsync(OmnichannelChannelEndpoint endpoint, UpdateEditorContext context)
    {
        if (GetMessagingCapabilities(endpoint).Count == 0)
        {
            return Edit(endpoint, context);
        }

        var model = new MessagingLineEndpointViewModel();

        await context.Updater.TryUpdateModelAsync(model, Prefix);

        // That an agent texts from one number per channel is enforced by MessagingLineEndpointRule, so a recipe is held
        // to it too.
        endpoint.Put(new MessagingLineSettings
        {
            UserIds = (model.TextingUserIds ?? [])
                .Where(userId => !string.IsNullOrWhiteSpace(userId))
                .Select(userId => userId.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList(),
        });

        return Edit(endpoint, context);
    }

    // The card applies to an address that can be used on a messaging channel: one with a messaging capability, or of a
    // type a messaging channel is offered for, so ticking SMS on a new number shows it straight away.
    private List<string> GetMessagingCapabilities(OmnichannelChannelEndpoint endpoint)
    {
        var addressType = endpoint.GetAddressType();

        return _channelResolver.GetAll()
            .Select(channel => channel.Name)
            .Where(name => endpoint.HasCapability(name) ||
                (_addressOptions.Capabilities.TryGetValue(name, out var capability) &&
                    string.Equals(capability.AddressType, addressType, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
