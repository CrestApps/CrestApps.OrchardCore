using CrestApps.Core;
using CrestApps.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// A reusable voice media library entry: a named audio clip (hold music, a voicemail greeting, an IVR prompt)
/// uploaded once and then referenced by id from queues, campaigns, entry points, or a global default. Storing the
/// clip once and referencing it keeps a business's audio in one place instead of duplicating an uploaded file on
/// every entity that plays it. The platform keeps the master copy of the audio in its own media store and gives
/// each telephony provider a copy when that provider first plays the clip, so the entry survives a change of
/// provider and the loss of a provider's copy.
/// </summary>
public sealed class VoiceMediaItem : CatalogItem, INameAwareModel, IModifiedUtcAwareModel, IVoiceMediaAsset
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
    /// Gets or sets the technical name of the telephony provider whose copy <see cref="MediaReference"/> plays (for
    /// example <c>Telnyx</c>). Kept for the readers, recipes and deployment plans written when an entry recorded a
    /// single provider copy; <see cref="ProviderReferences"/> is the record of every provider's copy, and the pair
    /// is folded into it when the entry is read.
    /// </summary>
    public string ProviderName { get; set; }

    /// <summary>
    /// Gets or sets the reference the provider named by <see cref="ProviderName"/> plays the clip by (for Telnyx,
    /// the Media Storage <c>media_name</c>). See <see cref="ProviderName"/> for why it is kept.
    /// </summary>
    public string MediaReference { get; set; }

    /// <inheritdoc/>
    public string Format { get; set; }

    /// <inheritdoc/>
    public string MasterStorageReference { get; set; }

    /// <inheritdoc/>
    public string ContentType { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string> ProviderReferences { get; set; } = new();

    /// <summary>
    /// Gets or sets the UTC time the clip was created.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the clip was last modified.
    /// </summary>
    public DateTime? ModifiedUtc { get; set; }
}
