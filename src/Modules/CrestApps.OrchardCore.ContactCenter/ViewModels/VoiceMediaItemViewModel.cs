using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// Represents the edit view model for a voice media library entry.
/// </summary>
public class VoiceMediaItemViewModel
{
    /// <summary>
    /// Gets or sets the media clip identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the unique media clip name.
    /// </summary>
    [Required]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the media clip description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the audio file being uploaded. It is kept as the clip's master copy and sent to the telephony
    /// provider. Optional on edit, where omitting it keeps the existing clip.
    /// </summary>
    public IFormFile Audio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry has audio: a master copy, a provider's copy, or both.
    /// </summary>
    public bool HasMedia { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the platform keeps a master copy of the audio. Without one the clip
    /// exists only at the provider it was uploaded to and has to be uploaded again to be protected.
    /// </summary>
    public bool HasMasterCopy { get; set; }

    /// <summary>
    /// Gets or sets the technical names of the telephony providers that hold a copy of the clip.
    /// </summary>
    public IList<string> ProviderNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the audio format of the clip (for example <c>mp3</c> or <c>wav</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether audio can be uploaded: a media store keeps master copies, a
    /// telephony provider capable of hosting media is configured, or both.
    /// </summary>
    public bool CanUpload { get; set; }
}
