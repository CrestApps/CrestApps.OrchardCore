namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// The outcome of re-syncing a voice media library entry to every enabled telephony provider that hosts media.
/// </summary>
public sealed class VoiceMediaResyncResult
{
    /// <summary>
    /// Gets a value indicating whether the entry has a master copy to re-sync from. Without one nothing can be
    /// done: the clip has to be uploaded again.
    /// </summary>
    public bool HasMasterCopy { get; init; }

    /// <summary>
    /// Gets what was done for each provider, keyed by the provider's technical name. Empty when no provider that
    /// hosts media is enabled.
    /// </summary>
    public IReadOnlyDictionary<string, VoiceMediaResyncOutcome> Providers { get; init; } = new Dictionary<string, VoiceMediaResyncOutcome>();
}
