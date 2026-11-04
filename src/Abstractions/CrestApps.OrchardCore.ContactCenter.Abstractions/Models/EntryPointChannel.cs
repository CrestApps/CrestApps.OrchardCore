using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.ContactCenter.Models;

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
