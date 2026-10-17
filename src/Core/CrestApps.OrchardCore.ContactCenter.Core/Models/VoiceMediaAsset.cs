namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The storage state of an audio clip that is part of another record rather than a library entry of its own, such
/// as the voicemail greeting an agent recorded. See <see cref="IVoiceMediaAsset"/>.
/// </summary>
public sealed class VoiceMediaAsset : IVoiceMediaAsset
{
    /// <inheritdoc/>
    public string MasterStorageReference { get; set; }

    /// <inheritdoc/>
    public string ContentType { get; set; }

    /// <inheritdoc/>
    public string Format { get; set; }

    /// <inheritdoc/>
    public Dictionary<string, string> ProviderReferences { get; set; } = new();
}
