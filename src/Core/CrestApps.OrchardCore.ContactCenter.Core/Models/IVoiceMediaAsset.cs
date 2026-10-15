namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// An audio clip the platform owns: where its master copy is kept, and which telephony providers hold a copy of it.
/// The master copy lives in the platform's own media store, so a provider's copy is only a cache that can be made
/// again when the provider changes, or when the provider loses the clip. A voice media library entry is one such
/// clip; an agent's recorded voicemail greeting is another.
/// </summary>
public interface IVoiceMediaAsset
{
    /// <summary>
    /// Gets or sets the reference of the master copy in the platform's media store, or <see langword="null"/> when
    /// the clip was uploaded before master copies were kept and exists only at a provider.
    /// </summary>
    string MasterStorageReference { get; set; }

    /// <summary>
    /// Gets or sets the content type the audio was uploaded with (for example <c>audio/mpeg</c>), which a provider
    /// is given again when the clip is uploaded from the master copy.
    /// </summary>
    string ContentType { get; set; }

    /// <summary>
    /// Gets or sets the audio format the clip was uploaded in (for example <c>mp3</c> or <c>wav</c>).
    /// </summary>
    string Format { get; set; }

    /// <summary>
    /// Gets or sets the copy each telephony provider holds, keyed by the provider's technical name (for example
    /// <c>Telnyx</c>) with the opaque reference the provider plays the clip by as the value.
    /// </summary>
    Dictionary<string, string> ProviderReferences { get; set; }
}
