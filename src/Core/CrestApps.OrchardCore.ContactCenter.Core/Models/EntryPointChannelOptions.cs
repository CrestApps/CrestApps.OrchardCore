using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

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

/// <summary>
/// A channel inbound entry points can answer.
/// </summary>
public sealed class EntryPointChannel
{
    /// <summary>
    /// Gets or sets the channel's name, which is the address capability it answers (for example <c>Phone</c>).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the localized name shown for the channel.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized description shown when choosing what kind of entry point to add.
    /// </summary>
    public LocalizedString Description { get; set; }
}
