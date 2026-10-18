using CrestApps.OrchardCore.ContactCenter.Core.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// Reconciles voice media library entries written before provider copies were tracked per provider with the shape
/// the running code expects.
/// </summary>
internal static class VoiceMediaItemCompatibility
{
    /// <summary>
    /// Adds the single provider copy recorded in <see cref="VoiceMediaItem.ProviderName"/> and
    /// <see cref="VoiceMediaItem.MediaReference"/> to <see cref="VoiceMediaItem.ProviderReferences"/> when that
    /// provider is not already in it, so the copy is played, re-synced and deleted like every other.
    /// </summary>
    /// <param name="item">The entry to reconcile.</param>
    public static void FoldLegacyReference(VoiceMediaItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        item.ProviderReferences ??= new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(item.ProviderName) || string.IsNullOrWhiteSpace(item.MediaReference))
        {
            return;
        }

        if (item.ProviderReferences.Keys.Any(key => string.Equals(key, item.ProviderName, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        item.ProviderReferences[item.ProviderName] = item.MediaReference;
    }
}
