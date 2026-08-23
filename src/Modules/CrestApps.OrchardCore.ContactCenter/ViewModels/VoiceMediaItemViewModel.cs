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
    /// Gets or sets the audio file being uploaded to the telephony provider's media storage. Optional on edit, where
    /// omitting it keeps the existing clip.
    /// </summary>
    public IFormFile Audio { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entry already references provider-hosted audio.
    /// </summary>
    public bool HasMedia { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the telephony provider that currently hosts the clip.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the audio format of the currently hosted clip (for example <c>mp3</c> or <c>wav</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a telephony provider capable of hosting media is configured, which
    /// determines whether audio can be uploaded.
    /// </summary>
    public bool CanUpload { get; set; }
}
