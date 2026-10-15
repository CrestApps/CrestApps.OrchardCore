using CrestApps.Core;
using CrestApps.Core.Support;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.ContactCenter.Core.Services;

/// <summary>
/// The default <see cref="IVoiceMediaProvisioningService"/>. Master copies are kept in the Telephony media store
/// (<see cref="IRecordingMediaStore"/>), so they land wherever recordings do: the tenant's application-data folder,
/// or Azure Blob Storage when that feature is enabled. Provider copies are made through each provider's
/// <see cref="IVoiceMediaProvisioner"/>.
/// </summary>
public sealed class VoiceMediaProvisioningService : IVoiceMediaProvisioningService
{
    private const string DefaultNamePrefix = "cc-voice-media";
    private const string MasterStorageKeyPrefix = "voice-media-";

    private readonly IVoiceMediaItemStore _store;
    private readonly IRecordingMediaStore _mediaStore;
    private readonly IVoiceMediaProvisioner[] _provisioners;
    private readonly IVoiceMediaAvailabilityCache _availability;
    private readonly IVoiceMediaUploadCoordinator _uploads;
    private readonly IVoiceMediaCopyRecorder _copyRecorder;
    private readonly IOptionsSnapshot<TelephonySettings> _telephonySettings;
    private readonly ILogger _logger;

    // The media store belongs to the Telephony feature and a provisioner to a provider feature. The voice media
    // library is registered by the base Contact Center feature, so either may be absent: without a store a clip is
    // held at the provider only, and without a provisioner it is held as a master copy only until a provider arrives.

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceMediaProvisioningService"/> class.
    /// </summary>
    /// <param name="store">The library store the entries are read from and written to.</param>
    /// <param name="mediaStores">The media store master copies are kept in, when the Telephony feature registers one.</param>
    /// <param name="provisioners">The provisioners of the enabled providers that host media.</param>
    /// <param name="availability">The memory of the copies providers have confirmed they hold.</param>
    /// <param name="uploads">The coordinator that runs one upload per clip and provider at a time.</param>
    /// <param name="copyRecorder">Records a copy made for a library entry on that entry after the caller's unit of work commits.</param>
    /// <param name="telephonySettings">The tenant telephony settings naming the default provider.</param>
    /// <param name="logger">The logger.</param>
    public VoiceMediaProvisioningService(
        IVoiceMediaItemStore store,
        IEnumerable<IRecordingMediaStore> mediaStores,
        IEnumerable<IVoiceMediaProvisioner> provisioners,
        IVoiceMediaAvailabilityCache availability,
        IVoiceMediaUploadCoordinator uploads,
        IVoiceMediaCopyRecorder copyRecorder,
        IOptionsSnapshot<TelephonySettings> telephonySettings,
        ILogger<VoiceMediaProvisioningService> logger)
    {
        _store = store;
        _mediaStore = mediaStores.FirstOrDefault();
        _provisioners = provisioners.ToArray();
        _availability = availability;
        _uploads = uploads;
        _copyRecorder = copyRecorder;
        _telephonySettings = telephonySettings;
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool CanUpload
        => _mediaStore is not null || _provisioners.Length > 0;

    /// <inheritdoc/>
    public string ActiveProviderName
        => GetActiveProvisioner()?.ProviderTechnicalName;

    /// <inheritdoc/>
    public async Task<VoiceMediaStoreResult> StoreAsync(IVoiceMediaAsset media, Stream audio, string contentType, string format, string namePrefix, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(audio);

        var provisioner = GetActiveProvisioner();

        if (_mediaStore is null && provisioner is null)
        {
            return new VoiceMediaStoreResult();
        }

        var replaced = VoiceMediaAssetCopies.HasAnyCopy(media) ? VoiceMediaAssetCopies.Snapshot(media) : null;

        // The store and the provider both read the audio from the start, so a stream that cannot be rewound is
        // buffered once rather than read twice.
        var content = audio;
        MemoryStream buffer = null;

        if (!audio.CanSeek)
        {
            buffer = new MemoryStream();
            await audio.CopyToAsync(buffer, cancellationToken);
            content = buffer;
        }

        try
        {
            content.Position = 0;

            var masterReference = await StoreMasterAsync(content, format, cancellationToken);
            string providerReference = null;
            var uploadFailed = false;

            if (provisioner is not null)
            {
                content.Position = 0;
                providerReference = await provisioner.UploadAsync(content, contentType, string.IsNullOrWhiteSpace(namePrefix) ? DefaultNamePrefix : namePrefix, cancellationToken);

                if (string.IsNullOrWhiteSpace(providerReference))
                {
                    providerReference = null;
                    uploadFailed = true;
                }
            }

            if (masterReference is null && providerReference is null)
            {
                return new VoiceMediaStoreResult
                {
                    ProviderName = provisioner?.ProviderTechnicalName,
                    ProviderUploadFailed = uploadFailed,
                };
            }

            // The audio changed, so every copy made of the old audio is stale: the record starts over with the one
            // copy made now, and any other provider is given a copy of the new audio when it next plays the clip.
            ForgetCopies(replaced);

            media.MasterStorageReference = masterReference;
            media.ContentType = contentType;
            media.Format = format;
            media.ProviderReferences = new Dictionary<string, string>();

            if (providerReference is not null)
            {
                media.ProviderReferences[provisioner.ProviderTechnicalName] = providerReference;
                _availability.MarkAvailable(provisioner.ProviderTechnicalName, providerReference);
            }

            return new VoiceMediaStoreResult
            {
                MasterStored = masterReference is not null,
                ProviderName = provisioner?.ProviderTechnicalName,
                ProviderReference = providerReference,
                ProviderUploadFailed = uploadFailed,
                Replaced = replaced,
            };
        }
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync();
            }
        }
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaProvisioningResult> EnsureProvisionedAsync(IVoiceMediaAsset media, string providerName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var existing = TryGetReference(media, providerName);

        if (!HasMasterCopy(media))
        {
            // Nothing to make a copy from: the copy the provider was given at upload is all there is, and a
            // provider that was never given one has nothing to play.
            return new VoiceMediaProvisioningResult
            {
                ProviderName = existing is null ? null : providerName,
                Reference = existing,
                Status = existing is null
                    ? VoiceMediaProvisioningStatus.NoAudio
                    : VoiceMediaProvisioningStatus.ProviderCopyOnly,
            };
        }

        var provisioner = FindProvisioner(providerName);

        if (provisioner is null)
        {
            // The provider cannot be given a copy from here, so whatever copy it holds is what plays.
            return new VoiceMediaProvisioningResult
            {
                ProviderName = existing is null ? null : providerName,
                Reference = existing,
                Status = existing is null
                    ? VoiceMediaProvisioningStatus.Unavailable
                    : VoiceMediaProvisioningStatus.Provisioned,
            };
        }

        if (existing is not null)
        {
            // A copy the provider confirmed within the window is trusted without asking again, so a play costs no
            // provider round trip beyond the play itself. The window starts when the provider answers and a play
            // inside it does not extend it, so however often the clip plays the provider is asked again once the
            // window ends, and a copy lost at the provider is noticed then.
            var known = _availability.IsKnownAvailable(provisioner.ProviderTechnicalName, existing);
            var availability = known
                ? VoiceMediaAvailability.Available
                : await provisioner.GetAvailabilityAsync(existing, cancellationToken);

            if (!known && availability == VoiceMediaAvailability.Available)
            {
                _availability.MarkAvailable(provisioner.ProviderTechnicalName, existing);
            }

            if (availability != VoiceMediaAvailability.Missing)
            {
                return new VoiceMediaProvisioningResult
                {
                    ProviderName = provisioner.ProviderTechnicalName,
                    Reference = existing,
                    Status = VoiceMediaProvisioningStatus.Provisioned,
                };
            }

            // The lost copy may be the one a recent upload produced; it must not be handed out again.
            _availability.Forget(provisioner.ProviderTechnicalName, existing);
            _uploads.Forget(provisioner.ProviderTechnicalName, media.MasterStorageReference);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    "The telephony provider {Provider} no longer holds the voice media copy {Reference}; it is uploaded again from the master copy.",
                    provisioner.ProviderTechnicalName,
                    existing.SanitizeLogValue());
            }
        }

        // Calls that need the clip at the same moment make one copy between them rather than one each.
        var reference = await _uploads.UploadOnceAsync(
            provisioner.ProviderTechnicalName,
            media.MasterStorageReference,
            token => UploadFromMasterAsync(media, provisioner, token),
            cancellationToken);

        if (reference is null)
        {
            return new VoiceMediaProvisioningResult
            {
                Status = VoiceMediaProvisioningStatus.Unavailable,
            };
        }

        SetReference(media, provisioner.ProviderTechnicalName, reference);
        _availability.MarkAvailable(provisioner.ProviderTechnicalName, reference);

        return new VoiceMediaProvisioningResult
        {
            ProviderName = provisioner.ProviderTechnicalName,
            Reference = reference,
            ReplacedReference = existing,
            Status = VoiceMediaProvisioningStatus.Provisioned,
            Changed = true,
        };
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaResolution> ResolveAsync(string mediaId, string providerName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(mediaId);
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        var item = await _store.FindByIdAsync(mediaId, cancellationToken);

        if (item is null)
        {
            return new VoiceMediaResolution
            {
                Status = VoiceMediaProvisioningStatus.NoAudio,
            };
        }

        var result = await EnsureProvisionedAsync(item, providerName, cancellationToken);

        // The copy is recorded on the entry after the caller's unit of work, not in it: a write here would silently
        // overwrite an operator's edit made while the upload ran, and could restore a master copy that edit replaced.
        if (result.Changed)
        {
            _copyRecorder.RecordLibraryCopy(item.ItemId, item.MasterStorageReference, result.ProviderName, result.Reference, result.ReplacedReference);
        }

        return new VoiceMediaResolution
        {
            Item = item,
            Reference = result.Reference,
            Status = result.Status,
        };
    }

    /// <inheritdoc/>
    public async Task<bool> VerifyProviderCopyAsync(string providerName, string mediaReference, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(providerName);

        if (string.IsNullOrWhiteSpace(mediaReference))
        {
            return false;
        }

        var provisioner = FindProvisioner(providerName);

        if (provisioner is null)
        {
            return false;
        }

        if (_availability.IsKnownAvailable(provisioner.ProviderTechnicalName, mediaReference))
        {
            return true;
        }

        if (await provisioner.GetAvailabilityAsync(mediaReference, cancellationToken) != VoiceMediaAvailability.Available)
        {
            return false;
        }

        _availability.MarkAvailable(provisioner.ProviderTechnicalName, mediaReference);

        return true;
    }

    /// <inheritdoc/>
    public async Task<VoiceMediaResyncResult> ResyncAsync(VoiceMediaItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!HasMasterCopy(item))
        {
            return new VoiceMediaResyncResult();
        }

        var outcomes = new Dictionary<string, VoiceMediaResyncOutcome>(StringComparer.OrdinalIgnoreCase);
        var changed = false;

        foreach (var provisioner in _provisioners)
        {
            var providerName = provisioner.ProviderTechnicalName;
            var existing = TryGetReference(item, providerName);
            var availability = VoiceMediaAvailability.Missing;

            if (existing is not null)
            {
                // A re-sync is the operator asking for certainty, so the provider is asked whatever is remembered.
                availability = await provisioner.GetAvailabilityAsync(existing, cancellationToken);

                if (availability == VoiceMediaAvailability.Available)
                {
                    _availability.MarkAvailable(providerName, existing);
                    outcomes[providerName] = VoiceMediaResyncOutcome.Verified;

                    continue;
                }

                _availability.Forget(providerName, existing);
                _uploads.Forget(providerName, item.MasterStorageReference);
            }

            // Through the same gate as a first-use upload, so a call that needs the clip while the operator re-syncs
            // it shares the copy made here rather than making a second one.
            var reference = await _uploads.UploadOnceAsync(
                providerName,
                item.MasterStorageReference,
                token => UploadFromMasterAsync(item, provisioner, token),
                cancellationToken);

            if (reference is null)
            {
                outcomes[providerName] = VoiceMediaResyncOutcome.Failed;

                continue;
            }

            // A copy the provider could not be asked about may still be there; the fresh copy replaces it, so the
            // old one is released rather than left behind.
            if (existing is not null && availability == VoiceMediaAvailability.Unknown)
            {
                await DeleteProviderCopyAsync(provisioner, existing, cancellationToken);
            }

            SetReference(item, providerName, reference);
            _availability.MarkAvailable(providerName, reference);
            outcomes[providerName] = VoiceMediaResyncOutcome.Uploaded;
            changed = true;
        }

        if (changed)
        {
            var active = GetActiveProvisioner();
            var activeReference = active is null ? null : TryGetReference(item, active.ProviderTechnicalName);

            if (activeReference is not null)
            {
                MirrorLegacyReference(item, active.ProviderTechnicalName, activeReference);
            }

            await _store.UpdateAsync(item, cancellationToken);
        }

        return new VoiceMediaResyncResult
        {
            HasMasterCopy = true,
            Providers = outcomes,
        };
    }

    /// <inheritdoc/>
    public async Task DeleteEverywhereAsync(IVoiceMediaAsset media, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(media);

        ForgetCopies(media);

        foreach (var (providerName, reference) in media.ProviderReferences ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(reference))
            {
                continue;
            }

            var provisioner = FindProvisioner(providerName);

            if (provisioner is null)
            {
                _logger.LogWarning(
                    "No media provisioner is registered for the telephony provider {Provider}, so its copy {Reference} of the deleted voice media was left in place.",
                    providerName.SanitizeLogValue(),
                    reference.SanitizeLogValue());

                continue;
            }

            // A copy a recent upload produced must not be handed out again once it is deleted.
            _uploads.Forget(providerName, media.MasterStorageReference);

            await DeleteProviderCopyAsync(provisioner, reference, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(media.MasterStorageReference))
        {
            return;
        }

        if (_mediaStore is null)
        {
            _logger.LogWarning(
                "No media store is registered, so the master copy {Reference} of the deleted voice media was left in place.",
                media.MasterStorageReference.SanitizeLogValue());

            return;
        }

        try
        {
            if (!await _mediaStore.DeleteAsync(media.MasterStorageReference, cancellationToken))
            {
                _logger.LogWarning(
                    "The media store could not confirm that the master copy {Reference} of the deleted voice media was removed.",
                    media.MasterStorageReference.SanitizeLogValue());
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to delete the master copy {Reference} of the deleted voice media.", media.MasterStorageReference.SanitizeLogValue());
        }
    }

    private async Task<string> StoreMasterAsync(Stream content, string format, CancellationToken cancellationToken)
    {
        if (_mediaStore is null)
        {
            return null;
        }

        try
        {
            return await _mediaStore.StoreAsync(new RecordingMediaWriteRequest
            {
                StorageKey = MasterStorageKeyPrefix + UniqueId.GenerateId(),
                Format = format,
                Content = content,
            }, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The clip can still be held at the provider, as it was before master copies existed; the entry then
            // shows as a provider copy only, which tells the operator to upload it again.
            _logger.LogError(ex, "Failed to store the master copy of uploaded voice media.");

            return null;
        }
    }

    private async Task<string> UploadFromMasterAsync(IVoiceMediaAsset media, IVoiceMediaProvisioner provisioner, CancellationToken cancellationToken)
    {
        Stream master;

        try
        {
            master = await _mediaStore.OpenReadAsync(media.MasterStorageReference, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to read the voice media master copy {Reference}.", media.MasterStorageReference.SanitizeLogValue());

            return null;
        }

        if (master is null)
        {
            _logger.LogWarning(
                "The voice media master copy {Reference} is not in the media store, so the telephony provider {Provider} could not be given a copy.",
                media.MasterStorageReference.SanitizeLogValue(),
                provisioner.ProviderTechnicalName);

            return null;
        }

        await using (master)
        {
            var reference = await provisioner.UploadAsync(master, ResolveContentType(media), DefaultNamePrefix, cancellationToken);

            if (string.IsNullOrWhiteSpace(reference))
            {
                _logger.LogWarning(
                    "The telephony provider {Provider} refused a copy of the voice media master {Reference}.",
                    provisioner.ProviderTechnicalName,
                    media.MasterStorageReference.SanitizeLogValue());

                return null;
            }

            return reference;
        }
    }

    private async Task DeleteProviderCopyAsync(IVoiceMediaProvisioner provisioner, string reference, CancellationToken cancellationToken)
    {
        try
        {
            await provisioner.DeleteAsync(reference, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete the voice media copy {Reference} from the telephony provider {Provider}.",
                reference.SanitizeLogValue(),
                provisioner.ProviderTechnicalName);
        }
    }

    private void ForgetCopies(IVoiceMediaAsset media)
    {
        if (media?.ProviderReferences is null)
        {
            return;
        }

        foreach (var (providerName, reference) in media.ProviderReferences)
        {
            _availability.Forget(providerName, reference);
        }
    }

    private bool HasMasterCopy(IVoiceMediaAsset media)
        => _mediaStore is not null && !string.IsNullOrWhiteSpace(media.MasterStorageReference);

    private IVoiceMediaProvisioner GetActiveProvisioner()
    {
        var defaultProviderName = _telephonySettings.Value?.DefaultProviderName;

        if (!string.IsNullOrEmpty(defaultProviderName))
        {
            var configured = FindProvisioner(defaultProviderName);

            if (configured is not null)
            {
                return configured;
            }
        }

        return _provisioners.FirstOrDefault();
    }

    private IVoiceMediaProvisioner FindProvisioner(string providerName)
        => _provisioners.FirstOrDefault(provisioner => string.Equals(provisioner.ProviderTechnicalName, providerName, StringComparison.OrdinalIgnoreCase));

    private static string TryGetReference(IVoiceMediaAsset media, string providerName)
    {
        if (media.ProviderReferences is null)
        {
            return null;
        }

        foreach (var (name, reference) in media.ProviderReferences)
        {
            if (string.Equals(name, providerName, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(reference))
            {
                return reference;
            }
        }

        return null;
    }

    private static void SetReference(IVoiceMediaAsset media, string providerName, string reference)
    {
        media.ProviderReferences ??= new Dictionary<string, string>();

        foreach (var key in media.ProviderReferences.Keys.Where(key => string.Equals(key, providerName, StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            media.ProviderReferences.Remove(key);
        }

        media.ProviderReferences[providerName] = reference;
    }

    // The single provider copy an entry recorded before copies were tracked per provider follows the copy a provider
    // was last given, so a reader of those members still finds a copy that plays.
    private static void MirrorLegacyReference(VoiceMediaItem item, string providerName, string reference)
    {
        if (string.IsNullOrEmpty(providerName) || string.IsNullOrEmpty(reference))
        {
            return;
        }

        item.ProviderName = providerName;
        item.MediaReference = reference;
    }

    private static string ResolveContentType(IVoiceMediaAsset media)
    {
        if (!string.IsNullOrWhiteSpace(media.ContentType))
        {
            return media.ContentType;
        }

        // Entries written before the content type was recorded know only their format.
        return media.Format?.Trim().ToLowerInvariant() switch
        {
            "mp3" => "audio/mpeg",
            "wav" => "audio/wav",
            "ogg" => "audio/ogg",
            "webm" => "audio/webm",
            _ => "application/octet-stream",
        };
    }
}
