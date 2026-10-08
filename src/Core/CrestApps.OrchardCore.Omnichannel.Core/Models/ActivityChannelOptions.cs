using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Lists the channels that can occur on activities, so the channel filters only offer channels an activity can
/// actually carry. A feature that lets activities use a channel registers it here.
/// </summary>
public sealed class ActivityChannelOptions
{
    private readonly Dictionary<string, ActivityChannelEntry> _channels = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the registered channels, keyed by the stored <see cref="OmnichannelActivity.Channel"/> value.
    /// </summary>
    public IReadOnlyDictionary<string, ActivityChannelEntry> Channels
        => _channels;

    /// <summary>
    /// Adds a channel, or updates the one already registered under the same value.
    /// </summary>
    /// <param name="channel">The stored <see cref="OmnichannelActivity.Channel"/> value.</param>
    /// <param name="configure">An optional configuration action.</param>
    public void AddChannel(string channel, Action<ActivityChannelEntry> configure = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);

        if (!_channels.TryGetValue(channel, out var entry))
        {
            entry = new ActivityChannelEntry(channel);
        }

        configure?.Invoke(entry);

        entry.DisplayName ??= new LocalizedString(channel, channel);

        _channels[channel] = entry;
    }
}

/// <summary>
/// Represents one channel offered by the activity channel filters.
/// </summary>
public sealed class ActivityChannelEntry
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityChannelEntry"/> class.
    /// </summary>
    /// <param name="channel">The stored channel value.</param>
    public ActivityChannelEntry(string channel)
    {
        ArgumentException.ThrowIfNullOrEmpty(channel);

        Channel = channel;
    }

    /// <summary>
    /// Gets the stored channel value.
    /// </summary>
    public string Channel { get; }

    /// <summary>
    /// Gets or sets the display name shown in the UI.
    /// </summary>
    public LocalizedString DisplayName { get; set; }
}
