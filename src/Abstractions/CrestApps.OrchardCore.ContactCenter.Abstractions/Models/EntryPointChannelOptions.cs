namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Holds the channels inbound entry points can answer, registered by the features that answer them: calls by Inbound
/// Voice, texts by the messaging workspace. Adding an entry point offers one per registered channel.
/// </summary>
public sealed class EntryPointChannelOptions
{
    /// <summary>
    /// Gets the registered channels, keyed by the address capability they answer (case-insensitive).
    /// </summary>
    public Dictionary<string, EntryPointChannel> Channels { get; } = new(StringComparer.OrdinalIgnoreCase);
}

