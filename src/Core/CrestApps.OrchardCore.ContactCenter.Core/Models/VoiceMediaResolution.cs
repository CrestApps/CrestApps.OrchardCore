namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// What a telephony provider is to play for a voice media library entry a queue, menu or transfer refers to by id.
/// </summary>
public sealed class VoiceMediaResolution
{
    /// <summary>
    /// Gets the library entry, or <see langword="null"/> when no entry has the identifier.
    /// </summary>
    public VoiceMediaItem Item { get; init; }

    /// <summary>
    /// Gets the reference the provider plays the clip by, or <see langword="null"/> when nothing can play.
    /// </summary>
    public string Reference { get; init; }

    /// <summary>
    /// Gets where the clip stands with the provider.
    /// </summary>
    public VoiceMediaProvisioningStatus Status { get; init; }
}
