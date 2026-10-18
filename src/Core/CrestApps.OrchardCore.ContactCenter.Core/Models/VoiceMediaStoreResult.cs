namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of storing newly uploaded audio: the master copy the platform keeps, and the copy given to the
/// active telephony provider.
/// </summary>
public sealed class VoiceMediaStoreResult
{
    /// <summary>
    /// Gets a value indicating whether the master copy was stored in the platform's media store.
    /// </summary>
    public bool MasterStored { get; init; }

    /// <summary>
    /// Gets the technical name of the provider the audio was offered to, or <see langword="null"/> when no
    /// provider that hosts media is enabled.
    /// </summary>
    public string ProviderName { get; init; }

    /// <summary>
    /// Gets the reference the provider plays the clip by, or <see langword="null"/> when the provider holds no copy.
    /// </summary>
    public string ProviderReference { get; init; }

    /// <summary>
    /// Gets a value indicating whether the provider refused the upload. The master copy, when it was stored, still
    /// stands, and the provider is given a copy from it when the clip is next needed.
    /// </summary>
    public bool ProviderUploadFailed { get; init; }

    /// <summary>
    /// Gets the copies the upload replaced, to be deleted once the record that owns the clip is saved, or
    /// <see langword="null"/> when the clip had no audio before.
    /// </summary>
    public IVoiceMediaAsset Replaced { get; init; }

    /// <summary>
    /// Gets a value indicating whether the audio is held somewhere: as a master copy, at the provider, or both.
    /// </summary>
    public bool Succeeded
        => MasterStored || !string.IsNullOrEmpty(ProviderReference);
}
