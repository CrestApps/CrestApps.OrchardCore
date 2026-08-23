using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A reusable voice media library entry: a named audio clip (hold music, a voicemail greeting, an IVR prompt)
/// uploaded once to the telephony provider's media storage and then referenced by id from queues, campaigns,
/// entry points, or a global default. Storing the clip once and referencing it keeps a business's audio in one
/// place instead of duplicating an uploaded file on every entity that plays it.
/// </summary>
public sealed class VoiceMediaItem : CatalogItem, INameAwareModel, IModifiedUtcAwareModel
{
    /// <summary>
    /// Gets or sets the unique, admin-facing name of the media clip.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets an optional description (for example the business or line the clip is for).
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the technical name of the telephony provider that hosts the clip (for example
    /// <c>Telnyx</c>). A clip is playable only through the provider that stores it.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the provider-hosted media reference used to play the clip (for Telnyx, the Media Storage
    /// <c>media_name</c>).
    /// </summary>
    public string MediaReference { get; set; }

    /// <summary>
    /// Gets or sets the audio format the clip was uploaded in (for example <c>mp3</c> or <c>wav</c>).
    /// </summary>
    public string Format { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the clip was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the clip was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
