using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;
using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Services;

/// <summary>
/// Makes the endpoints of every messaging channel store their address in the channel's normalized form and refuse an
/// address the channel cannot deliver from. Inbound traffic finds its endpoint by exact address after the channel has
/// normalized it, so an endpoint saved as typed (a mailbox in mixed case, a number with a prefix) would never match
/// its own inbound. This is what lets a new channel's endpoints work without the channel writing any endpoint code.
/// </summary>
internal sealed class MessagingChannelEndpointAddressPolicy : IChannelEndpointAddressPolicy
{
    // The channel-endpoint handler canonicalizes and validates these itself, with messages specific to them.
    private static readonly HashSet<string> _governedByOmnichannel = new(StringComparer.OrdinalIgnoreCase)
    {
        OmnichannelConstants.Channels.Phone,
        OmnichannelConstants.Channels.Sms,
        OmnichannelConstants.Channels.Email,
    };

    private readonly IMessagingChannelResolver _channelResolver;

    internal readonly IStringLocalizer S;

    public MessagingChannelEndpointAddressPolicy(
        IMessagingChannelResolver channelResolver,
        IStringLocalizer<MessagingChannelEndpointAddressPolicy> stringLocalizer)
    {
        _channelResolver = channelResolver;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public bool AppliesTo(string channel)
        => !string.IsNullOrEmpty(channel) &&
            !_governedByOmnichannel.Contains(channel) &&
            _channelResolver.Get(channel) is not null;

    /// <inheritdoc/>
    public string Normalize(string channel, string address)
        => _channelResolver.Get(channel)?.NormalizeAddress(address) ?? address;

    /// <inheritdoc/>
    public LocalizedString Validate(string channel, string address)
    {
        var messagingChannel = _channelResolver.Get(channel);

        return messagingChannel is null || messagingChannel.IsValidAddress(address)
            ? null
            : S["This is not a valid {0} address.", messagingChannel.DisplayName];
    }
}
