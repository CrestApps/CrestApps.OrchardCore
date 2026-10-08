using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;

/// <summary>
/// The default <see cref="IMessagingContactResolver"/>: asks the address's own channel which contacts own it. When
/// several contacts share an address the first is linked, and the workspace lists the others beside the thread.
/// </summary>
public sealed class MessagingContactResolver : IMessagingContactResolver
{
    private readonly IMessagingChannelResolver _channelResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingContactResolver"/> class.
    /// </summary>
    /// <param name="channelResolver">The resolver of the enabled channels.</param>
    public MessagingContactResolver(IMessagingChannelResolver channelResolver)
    {
        _channelResolver = channelResolver;
    }

    /// <inheritdoc/>
    public async ValueTask<string> ResolveContactContentItemIdAsync(string channel, string address, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var messagingChannel = _channelResolver.Get(channel);

        if (messagingChannel is null)
        {
            return null;
        }

        var contactIds = await messagingChannel.FindContactIdsAsync(messagingChannel.NormalizeAddress(address), cancellationToken);

        return contactIds.Count > 0 ? contactIds[0] : null;
    }
}
