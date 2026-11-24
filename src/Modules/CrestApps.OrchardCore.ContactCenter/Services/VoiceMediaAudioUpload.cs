using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Drivers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.Environment.Shell.Scope;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// What became of an upload of a voice media clip's audio.
/// </summary>
internal enum VoiceMediaAudioUploadStatus
{
    /// <summary>
    /// The audio was stored, as a master copy, a provider copy or both.
    /// </summary>
    Stored,

    /// <summary>
    /// The audio is larger than <see cref="VoiceMediaAudioUpload.MaxAudioBytes"/>.
    /// </summary>
    TooLarge,

    /// <summary>
    /// The audio's content type is not one the library accepts.
    /// </summary>
    UnsupportedFormat,

    /// <summary>
    /// Neither media storage nor a telephony provider that hosts media is configured.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Nothing could be stored.
    /// </summary>
    Failed,
}

/// <summary>
/// The outcome of an upload of a voice media clip's audio.
/// </summary>
/// <param name="Status">What became of the upload.</param>
/// <param name="Store">What was stored, when the audio reached the provisioning service.</param>
internal sealed record VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus Status, VoiceMediaStoreResult Store)
{
    /// <summary>
    /// Gets whether the audio was stored.
    /// </summary>
    public bool Succeeded
        => Status == VoiceMediaAudioUploadStatus.Stored;
}

/// <summary>
/// Gives a voice media clip new audio: the size and format checks of the voice media editor, the master and provider
/// copies, and, once the clip is saved, the deletion of the copies the new audio replaced. The editor and the voice
/// media AI tool both upload through it, so both are held to the same rules.
/// </summary>
internal static class VoiceMediaAudioUpload
{
    /// <summary>
    /// The largest audio a clip may have. Telnyx Media Storage caps media at 20 MB; a greeting or hold-music clip sits
    /// well under this, so anything larger is not voice media.
    /// </summary>
    public const long MaxAudioBytes = 20L * 1024 * 1024;

    // Labels the provider's copy by purpose so provider media storage stays identifiable.
    private const string MediaNamePrefix = "cc-voice-media";

    private static readonly string[] _allowedContentTypes =
    [
        "audio/mpeg",
        "audio/mp3",
        "audio/wav",
        "audio/x-wav",
        "audio/wave",
        "audio/webm",
        "audio/ogg",
    ];

    /// <summary>
    /// Determines whether the library accepts audio of a content type.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns><see langword="true"/> when the content type is accepted.</returns>
    public static bool IsAllowedContentType(string contentType)
        => _allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Stores new audio for a clip and points the clip at it. The copies the new audio replaces are deleted once the
    /// clip is saved, and the new copies are deleted instead when it is not: the reconciliation runs after the shell
    /// scope's unit of work, so neither a failed save nor a refused form leaves the stored clip pointing at audio that
    /// is gone.
    /// </summary>
    /// <param name="provisioning">The service that keeps the master copy and gives the provider its copy.</param>
    /// <param name="item">The clip, which is changed to point at the new audio.</param>
    /// <param name="audio">The audio.</param>
    /// <param name="length">The audio's length in bytes.</param>
    /// <param name="contentType">The audio's content type.</param>
    /// <param name="format">The audio's format, for example <c>mp3</c>.</param>
    /// <param name="cancellationToken">A token to cancel the upload.</param>
    /// <returns>What became of the upload.</returns>
    public static async Task<VoiceMediaAudioUploadResult> UploadAsync(
        IVoiceMediaProvisioningService provisioning,
        VoiceMediaItem item,
        Stream audio,
        long length,
        string contentType,
        string format,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provisioning);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(audio);

        if (length > MaxAudioBytes)
        {
            return new VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus.TooLarge, null);
        }

        if (!IsAllowedContentType(contentType))
        {
            return new VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus.UnsupportedFormat, null);
        }

        if (!provisioning.CanUpload)
        {
            return new VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus.Unavailable, null);
        }

        var result = await provisioning.StoreAsync(item, audio, contentType, format, MediaNamePrefix, cancellationToken);

        if (!result.Succeeded)
        {
            return new VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus.Failed, result);
        }

        // The single provider copy recorded before copies were tracked per provider follows the copy the active
        // provider was just given, so a reader of those members still finds a copy that plays.
        item.ProviderName = result.ProviderReference is null ? null : result.ProviderName;
        item.MediaReference = result.ProviderReference;

        ShellScope.AddDeferredTask(scope => ReconcileReplacedAudioAsync(scope, item.ItemId, VoiceMediaAssetCopies.Snapshot(item), result.Replaced));

        return new VoiceMediaAudioUploadResult(VoiceMediaAudioUploadStatus.Stored, result);
    }

    private static async Task ReconcileReplacedAudioAsync(ShellScope scope, string itemId, VoiceMediaAsset uploaded, IVoiceMediaAsset replaced)
    {
        var provisioning = scope.ServiceProvider.GetRequiredService<IVoiceMediaProvisioningService>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<VoiceMediaItemDisplayDriver>>();

        try
        {
            var store = scope.ServiceProvider.GetRequiredService<IVoiceMediaItemStore>();
            var stored = string.IsNullOrEmpty(itemId) ? null : await store.FindByIdAsync(itemId);

            if (VoiceMediaAssetCopies.HoldsSameAudio(stored, uploaded))
            {
                if (replaced is not null)
                {
                    await provisioning.DeleteEverywhereAsync(replaced);
                }

                return;
            }

            // The entry was not saved with the new audio, so the copies made for it would never be played.
            await provisioning.DeleteEverywhereAsync(uploaded);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to delete the voice media audio replaced by an upload to the entry {ItemId}.", itemId.SanitizeLogValue());
        }
    }
}
