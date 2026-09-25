namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// Looks up the messaging channels the tenant has enabled.
/// </summary>
public interface IMessagingChannelResolver
{
    /// <summary>
    /// Gets every enabled channel, in display order.
    /// </summary>
    /// <returns>The enabled channels.</returns>
    IReadOnlyList<IMessagingChannel> GetAll();

    /// <summary>
    /// Gets the enabled channel with the specified technical name.
    /// </summary>
    /// <param name="name">The channel's technical name, compared without regard to case.</param>
    /// <returns>The channel, or <see langword="null"/> when no enabled channel has that name.</returns>
    IMessagingChannel Get(string name);
}
