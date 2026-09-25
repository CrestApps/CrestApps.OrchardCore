namespace CrestApps.OrchardCore.Omnichannel.Messaging.Core.Channels;

/// <summary>
/// The default <see cref="IMessagingChannelResolver"/>, over the channels registered in the container.
/// </summary>
public sealed class MessagingChannelResolver : IMessagingChannelResolver
{
    private readonly IReadOnlyList<IMessagingChannel> _channels;
    private readonly Dictionary<string, IMessagingChannel> _channelsByName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagingChannelResolver"/> class.
    /// </summary>
    /// <param name="channels">The registered channels.</param>
    public MessagingChannelResolver(IEnumerable<IMessagingChannel> channels)
    {
        _channels = channels
            .OrderBy(channel => channel.Order)
            .ThenBy(channel => channel.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _channelsByName = new Dictionary<string, IMessagingChannel>(StringComparer.OrdinalIgnoreCase);

        foreach (var channel in _channels)
        {
            // The first registration wins, so a channel registered twice cannot silently swap implementations.
            _channelsByName.TryAdd(channel.Name, channel);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<IMessagingChannel> GetAll()
        => _channels;

    /// <inheritdoc/>
    public IMessagingChannel Get(string name)
        => !string.IsNullOrEmpty(name) && _channelsByName.TryGetValue(name, out var channel)
            ? channel
            : null;
}
