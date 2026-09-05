namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Holds the channel-endpoint sources registered across the enabled features, keyed by channel name (the source
/// key). The channel-endpoint administration reads this to build the create picker and to validate the channel
/// an endpoint is created for.
/// </summary>
public sealed class ChannelEndpointSourceOptions
{
    /// <summary>
    /// Gets the registered sources, keyed by channel name (case-insensitive).
    /// </summary>
    public Dictionary<string, ChannelEndpointSource> Sources { get; } = new(StringComparer.OrdinalIgnoreCase);
}
