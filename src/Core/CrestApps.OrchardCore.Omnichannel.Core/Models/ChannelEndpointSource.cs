using Microsoft.Extensions.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Core.Models;

/// <summary>
/// Describes a channel-endpoint source (a channel such as SMS or Phone) that a feature has registered. The
/// source drives the create experience: the "Add endpoint" picker lists the registered sources, and the editor
/// is built by the display drivers that target that channel. Registering a source is how a feature opts a
/// channel into the channel-endpoint administration.
/// </summary>
public sealed class ChannelEndpointSource
{
    /// <summary>
    /// Gets or sets the localized display name shown for the source in the create picker.
    /// </summary>
    public LocalizedString DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the localized description shown for the source in the create picker.
    /// </summary>
    public LocalizedString Description { get; set; }
}
