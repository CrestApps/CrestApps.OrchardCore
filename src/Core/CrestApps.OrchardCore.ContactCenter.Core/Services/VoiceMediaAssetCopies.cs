using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reads the copies a clip is held as, so the code that stores a clip and the code that cleans up after it agree on
/// what "the same audio" means.
/// </summary>
internal static class VoiceMediaAssetCopies
{
    /// <summary>
    /// Takes a detached copy of the clip's storage state: the master copy reference and every provider copy.
    /// </summary>
    /// <param name="media">The clip.</param>
    /// <returns>A new asset carrying the same references; never <see langword="null"/>.</returns>
    public static VoiceMediaAsset Snapshot(IVoiceMediaAsset media)
    {
        ArgumentNullException.ThrowIfNull(media);

        return new VoiceMediaAsset
        {
            MasterStorageReference = media.MasterStorageReference,
            ContentType = media.ContentType,
            Format = media.Format,
            ProviderReferences = media.ProviderReferences is null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(media.ProviderReferences),
        };
    }

    /// <summary>
    /// Gets a value indicating whether the clip holds any audio: a master copy, a provider copy, or both.
    /// </summary>
    /// <param name="media">The clip.</param>
    /// <returns><see langword="true"/> when at least one copy is referenced.</returns>
    public static bool HasAnyCopy(IVoiceMediaAsset media)
        => media is not null
            && (!string.IsNullOrWhiteSpace(media.MasterStorageReference)
                || media.ProviderReferences?.Values.Any(reference => !string.IsNullOrWhiteSpace(reference)) == true);

    /// <summary>
    /// Gets a value indicating whether a stored clip holds the audio that was uploaded: the same master copy, or,
    /// when no master copy could be kept, every provider copy the upload made.
    /// </summary>
    /// <param name="stored">The clip as it is stored now.</param>
    /// <param name="uploaded">The clip as the upload left it.</param>
    /// <returns><see langword="true"/> when the stored clip is the uploaded one.</returns>
    public static bool HoldsSameAudio(IVoiceMediaAsset stored, IVoiceMediaAsset uploaded)
    {
        if (stored is null || uploaded is null)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(uploaded.MasterStorageReference))
        {
            return string.Equals(stored.MasterStorageReference, uploaded.MasterStorageReference, StringComparison.Ordinal);
        }

        return uploaded.ProviderReferences is { Count: > 0 }
            && stored.ProviderReferences is not null
            && uploaded.ProviderReferences.All(copy => stored.ProviderReferences.TryGetValue(copy.Key, out var reference) && string.Equals(reference, copy.Value, StringComparison.Ordinal));
    }
}
